using Godot;
using Grasp.Logger;

namespace Grasp.Robot;

public static class IkSolver
{
    private const float Delta = 0.0001f;
    private const float Lambda = 0.5f;

    public static float[]? Solve(
        JointPivot[] joints,
        Vector3 targetPos,
        Basis targetBasis,
        float[]? initialAngles = null,
        int maxIterations = 200,
        float posTolerance = 0.001f,
        float rotTolerance = 0.01f,
        bool positionOnly = false)
    {
        int n = joints.Length;
        var angles = new float[n];

        if (initialAngles != null)
        {
            for (int i = 0; i < Mathf.Min(initialAngles.Length, n); i++)
                angles[i] = Mathf.Clamp(initialAngles[i], joints[i].LowerLimit, joints[i].UpperLimit);
        }

        for (int iter = 0; iter < maxIterations; iter++)
        {
            var eePose = ForwardKinematics.ComputeEePose(joints, angles);
            Vector3 posError = targetPos - eePose.Origin;
            Basis rotErrorBasis = targetBasis * eePose.Basis.Inverse();


            // Extract rotation error as axis-angle
            var rotErrorQuat = rotErrorBasis.GetRotationQuaternion();
            float rotAngle = 2f * Mathf.Atan2(new Vector3(rotErrorQuat.X, rotErrorQuat.Y, rotErrorQuat.Z).Length(), rotErrorQuat.W);
            Vector3 rotAxis = rotAngle > 0.0001f
                ? new Vector3(rotErrorQuat.X, rotErrorQuat.Y, rotErrorQuat.Z).Normalized()
                : Vector3.Zero;
            Vector3 rotErrorVec = rotAxis * rotAngle;

            float posErr = posError.Length();
            float rotErr = rotAngle;

            if (posErr < posTolerance && (positionOnly || rotErr < rotTolerance))
            {
                Logger.Logger.Instance.Info("IkSolver",
                    $"Converged in {iter + 1} iters, posErr={posErr:F4}m, rotErr={Mathf.RadToDeg(rotErr):F2}deg");
                return angles;
            }

            int rows = positionOnly ? 3 : 6;

            // Build Jacobian numerically
            var jacobian = new float[rows, n];
            for (int j = 0; j < n; j++)
            {
                var perturbed = (float[])angles.Clone();
                perturbed[j] += Delta;
                var eePerturbed = ForwardKinematics.ComputeEePose(joints, perturbed);

                Vector3 dp = (eePerturbed.Origin - eePose.Origin) / Delta;
                jacobian[0, j] = dp.X;
                jacobian[1, j] = dp.Y;
                jacobian[2, j] = dp.Z;

                if (!positionOnly)
                {
                    Basis dB = eePerturbed.Basis * eePose.Basis.Inverse();
                    var dQuat = dB.GetRotationQuaternion();
                    float dAngle = 2f * Mathf.Atan2(new Vector3(dQuat.X, dQuat.Y, dQuat.Z).Length(), dQuat.W);
                    Vector3 dAxis = dAngle > 0.00001f
                        ? new Vector3(dQuat.X, dQuat.Y, dQuat.Z).Normalized()
                        : Vector3.Zero;
                    Vector3 dr = dAxis * dAngle / Delta;
                    jacobian[3, j] = dr.X;
                    jacobian[4, j] = dr.Y;
                    jacobian[5, j] = dr.Z;
                }
            }

            // Error vector
            var error = new float[rows];
            error[0] = posError.X; error[1] = posError.Y; error[2] = posError.Z;
            if (!positionOnly)
            {
                error[3] = rotErrorVec.X; error[4] = rotErrorVec.Y; error[5] = rotErrorVec.Z;
            }

            // Damped least squares: dq = Jt * (J*Jt + lambda*I)^-1 * e
            var jjt = new float[rows, rows];
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < rows; c++)
                {
                    float sum = 0;
                    for (int k = 0; k < n; k++)
                        sum += jacobian[r, k] * jacobian[c, k];
                    jjt[r, c] = sum;
                    if (r == c) jjt[r, c] += Lambda * Lambda;
                }

            // Solve (J*Jt + lambda*I) * x = e
            var x = SolveLinear(jjt, error, rows);

            // dq = Jt * x
            var dq = new float[n];
            for (int j = 0; j < n; j++)
            {
                float sum = 0;
                for (int r = 0; r < rows; r++)
                    sum += jacobian[r, j] * x[r];
                dq[j] = sum;
            }

            // Update angles
            for (int j = 0; j < n; j++)
                angles[j] = Mathf.Clamp(angles[j] + dq[j], joints[j].LowerLimit, joints[j].UpperLimit);
        }

        // Did not converge
        var finalPose = ForwardKinematics.ComputeEePose(joints, angles);
        Vector3 finalPosErr = targetPos - finalPose.Origin;
        Basis finalRotBasis = targetBasis * finalPose.Basis.Inverse();
        var finalRotQuat = finalRotBasis.GetRotationQuaternion();
        float finalRotAngle = 2f * Mathf.Atan2(new Vector3(finalRotQuat.X, finalRotQuat.Y, finalRotQuat.Z).Length(), finalRotQuat.W);
        Logger.Logger.Instance.Warn("IkSolver",
            $"Failed after {maxIterations} iters, posErr={finalPosErr.Length():F4}m, rotErr={Mathf.RadToDeg(finalRotAngle):F1}deg " +
            $"| target=({targetPos.X:F3},{targetPos.Y:F3},{targetPos.Z:F3}) " +
            $"reached=({finalPose.Origin.X:F3},{finalPose.Origin.Y:F3},{finalPose.Origin.Z:F3})");
        return null;
    }

    private static float[] SolveLinear(float[,] a, float[] b, int n)
    {
        var aug = new float[n, n + 1];
        for (int r = 0; r < n; r++)
        {
            for (int c = 0; c < n; c++)
                aug[r, c] = a[r, c];
            aug[r, n] = b[r];
        }

        for (int col = 0; col < n; col++)
        {
            // Pivot
            int maxRow = col;
            float maxVal = Mathf.Abs(aug[col, col]);
            for (int r = col + 1; r < n; r++)
            {
                if (Mathf.Abs(aug[r, col]) > maxVal)
                {
                    maxVal = Mathf.Abs(aug[r, col]);
                    maxRow = r;
                }
            }
            if (maxRow != col)
            {
                for (int c = 0; c <= n; c++)
                    (aug[col, c], aug[maxRow, c]) = (aug[maxRow, c], aug[col, c]);
            }

            float pivot = aug[col, col];
            if (Mathf.Abs(pivot) < 1e-10f) continue;

            for (int r = col + 1; r < n; r++)
            {
                float factor = aug[r, col] / pivot;
                for (int c = col; c <= n; c++)
                    aug[r, c] -= factor * aug[col, c];
            }
        }

        // Back substitution
        var result = new float[n];
        for (int r = n - 1; r >= 0; r--)
        {
            float sum = aug[r, n];
            for (int c = r + 1; c < n; c++)
                sum -= aug[r, c] * result[c];
            result[r] = Mathf.Abs(aug[r, r]) > 1e-10f ? sum / aug[r, r] : 0;
        }
        return result;
    }
}

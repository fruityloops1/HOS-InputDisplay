using Raylib_cs;
using System.Numerics;

namespace InputDisplay;

class MathUtil
{
    public static Quaternion ToQuaternion(float[,] m)
    {
        Quaternion q;

        float trace = m[0, 0] + m[1, 1] + m[2, 2];
        if (trace > 0)
        {
            float s = 0.5f / MathF.Sqrt(trace + 1.0f);
            q.W = 0.25f / s;
            q.X = (m[2, 1] - m[1, 2]) * s;
            q.Y = (m[0, 2] - m[2, 0]) * s;
            q.Z = (m[1, 0] - m[0, 1]) * s;
        }
        else
        {
            if (m[0, 0] > m[1, 1] && m[0, 0] > m[2, 2])
            {
                float s = 2.0f * MathF.Sqrt(1.0f + m[0, 0] - m[1, 1] - m[2, 2]);
                q.W = (m[2, 1] - m[1, 2]) / s;
                q.X = 0.25f * s;
                q.Y = (m[0, 1] + m[1, 0]) / s;
                q.Z = (m[0, 2] + m[2, 0]) / s;
            }
            else if (m[1, 1] > m[2, 2])
            {
                float s = 2.0f * MathF.Sqrt(1.0f + m[1, 1] - m[0, 0] - m[2, 2]);
                q.W = (m[0, 2] - m[2, 0]) / s;
                q.X = (m[0, 1] + m[1, 0]) / s;
                q.Y = 0.25f * s;
                q.Z = (m[1, 2] + m[2, 1]) / s;
            }
            else
            {
                float s = 2.0f * MathF.Sqrt(1.0f + m[2, 2] - m[0, 0] - m[1, 1]);
                q.W = (m[1, 0] - m[0, 1]) / s;
                q.X = (m[0, 2] + m[2, 0]) / s;
                q.Y = (m[1, 2] + m[2, 1]) / s;
                q.Z = 0.25f * s;
            }
        }

        return q;
    }
}
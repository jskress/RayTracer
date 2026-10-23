using RayTracer.Basics;

namespace RayTracer.Core.Physics;

/// <summary>
/// This struct holds a turn -- a rotation -- as a unit quaternion, in doubles, which is what a body's orientation
/// is kept as while it is moved: it composes and steps without drifting out of being a rotation, as a matrix does.
/// </summary>
public readonly record struct Turn(double W, double X, double Y, double Z)
{
    /// <summary>
    /// This is the turn that turns nothing.
    /// </summary>
    public static readonly Turn None = new (1, 0, 0, 0);

    /// <summary>
    /// This method returns the turn about an axis by an angle in radians.
    /// </summary>
    public static Turn About(Vector axis, double angle)
    {
        double length = axis.Magnitude;

        if (length < 1e-15)
            return None;

        double half = angle / 2, s = Math.Sin(half) / length;

        return new Turn(Math.Cos(half), axis.X * s, axis.Y * s, axis.Z * s);
    }

    /// <summary>
    /// This method returns the least turn that carries one direction onto another.
    /// </summary>
    public static Turn Between(Vector from, Vector to)
    {
        Vector a = from.Unit, b = to.Unit;
        double cosine = Math.Clamp(a.Dot(b), -1, 1);
        Vector axis = a.Cross(b);

        if (axis.Magnitude < 1e-12)
        {
            if (cosine > 0)
                return None;

            // Turned right round: about any axis across it.
            Vector across = Math.Abs(a.X) < 0.9 ? new Vector(1, 0, 0) : new Vector(0, 1, 0);

            return About(a.Cross(across), Math.PI);
        }

        return About(axis, Math.Acos(cosine));
    }

    public static Turn operator *(Turn a, Turn b) => new (
        a.W * b.W - a.X * b.X - a.Y * b.Y - a.Z * b.Z,
        a.W * b.X + a.X * b.W + a.Y * b.Z - a.Z * b.Y,
        a.W * b.Y - a.X * b.Z + a.Y * b.W + a.Z * b.X,
        a.W * b.Z + a.X * b.Y - a.Y * b.X + a.Z * b.W);

    /// <summary>
    /// This method returns the turn made unit again.
    /// </summary>
    public Turn Normalized()
    {
        double length = Math.Sqrt(W * W + X * X + Y * Y + Z * Z);

        return length > 0 ? new Turn(W / length, X / length, Y / length, Z / length) : None;
    }

    /// <summary>
    /// This method returns the turn after turning at an angular velocity, in radians a second, for a time.
    /// </summary>
    public Turn Stepped(Vector spin, double time)
    {
        double rate = spin.Magnitude;

        return rate < 1e-15 ? this : (About(spin, rate * time) * this).Normalized();
    }

    /// <summary>
    /// This method returns a vector turned.
    /// </summary>
    public Vector Rotate(Vector vector)
    {
        Vector axis = new (X, Y, Z);
        Vector t = axis.Cross(vector) * 2;

        return vector + t * W + axis.Cross(t);
    }

    /// <summary>
    /// This method returns the way between this turn and another, a share of the way along.
    /// </summary>
    public Turn Toward(Turn other, double share)
    {
        double dot = W * other.W + X * other.X + Y * other.Y + Z * other.Z;
        Turn end = dot < 0 ? new Turn(-other.W, -other.X, -other.Y, -other.Z) : other;

        dot = Math.Abs(dot);

        if (dot > 0.9995)
        {
            return new Turn(W + (end.W - W) * share, X + (end.X - X) * share, Y + (end.Y - Y) * share,
                Z + (end.Z - Z) * share).Normalized();
        }

        double angle = Math.Acos(dot), sine = Math.Sin(angle);
        double a = Math.Sin((1 - share) * angle) / sine, b = Math.Sin(share * angle) / sine;

        return new Turn(W * a + end.W * b, X * a + end.X * b, Y * a + end.Y * b, Z * a + end.Z * b);
    }

    /// <summary>
    /// This method returns the turn as a matrix.
    /// </summary>
    public Matrix ToMatrix()
    {
        double xx = X * X, yy = Y * Y, zz = Z * Z, xy = X * Y, xz = X * Z, yz = Y * Z, wx = W * X, wy = W * Y, wz = W * Z;

        return new Matrix([
            1 - 2 * (yy + zz), 2 * (xy - wz), 2 * (xz + wy), 0,
            2 * (xy + wz), 1 - 2 * (xx + zz), 2 * (yz - wx), 0,
            2 * (xz - wy), 2 * (yz + wx), 1 - 2 * (xx + yy), 0,
            0, 0, 0, 1
        ]);
    }
}

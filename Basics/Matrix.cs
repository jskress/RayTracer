using System.Text;

namespace RayTracer.Basics;

/// <summary>
/// This class represents a 4x4 matrix.
/// </summary>
public class Matrix
{
    /// <summary>
    /// This field holds the 4x4 identity matrix.
    /// </summary>
    public static readonly Matrix Identity = new ();

    /// <summary>
    /// This property returns the determinant of this matrix.
    /// </summary>
    public double Determinant => CalculateDeterminant();

    /// <summary>
    /// This property indicates whether this matrix can be inverted.
    /// </summary>
    public bool CanInvert => Determinant != 0;

    private readonly double[] _data;
    private readonly int _extent;

    public Matrix() : this(new double[]
    {
        1, 0, 0, 0,
        0, 1, 0, 0,
        0, 0, 1, 0,
        0, 0, 0, 1
    }) {}

    public Matrix(int extent)
    {
        _data = new double[extent * extent];
        _extent = extent;
    }

    public Matrix(double[] data)
    {
        _data = data;
        _extent = Convert.ToInt32(Math.Sqrt(_data.Length));
    }

    /// <summary>
    /// This method returns the entry in the matrix at the given row and column.
    /// </summary>
    /// <param name="row">The row of the desired entry.</param>
    /// <param name="column">The column of the desired entry.</param>
    /// <returns>The value of the requested entry.</returns>
    public double Entry(int row, int column)
    {
        return _data[row * _extent + column];
    }

    /// <summary>
    /// This method is used to set the value of a specific entry in the matrix.
    /// </summary>
    /// <param name="row">The row of the entry to set the value of.</param>
    /// <param name="column">The column of the entry to set the value of.</param>
    /// <param name="newValue">The value to set the entry to.</param>
    /// <returns>This matrix, for fluency.</returns>
    public Matrix SetEntry(int row, int column, double newValue)
    {
        _data[row * _extent + column] = newValue;

        return this;
    }

    /// <summary>
    /// This method creates the transpose of this matrix.
    /// </summary>
    /// <returns>The transpose of this method.</returns>
    public Matrix Transpose()
    {
        Matrix result = new (_extent);

        for (int row = 0; row < _extent; row++)
        {
            for (int column = 0; column < _extent; column++)
            {
                int sourceIndex = row * _extent + column;
                int targetIndex = column * _extent + row;

                result._data[targetIndex] = _data[sourceIndex];
            }
        }

        return result;
    }

    /// <summary>
    /// This method is used to calculate the determinant of this matrix.
    /// </summary>
    /// <returns>The determinant of the matrix.</returns>
    private double CalculateDeterminant()
    {
        if (_extent == 2)
        {
            return
                Entry(0, 0) * Entry(1, 1) -
                Entry(0, 1) * Entry(1, 0);
        }

        double result = 0;

        for (int column = 0; column < _extent; column++)
            result += _data[column] * GetCofactor(0, column);

        return result;
    }

    /// <summary>
    /// This method determines the sub-matrix of this matrix by removing the specified row
    /// and column.
    /// </summary>
    /// <param name="row">The row to remove.</param>
    /// <param name="column">The column to remove.</param>
    /// <returns>The sub-matrix of this matrix that is the result of removing the indicated
    /// row and column.</returns>
    public Matrix GetSubMatrix(int row, int column)
    {
        Matrix result = new (_extent - 1);
        int index = 0;

        for (int r = 0; r < _extent; r++)
        {
            if (r != row)
            {
                for (int c = 0; c < _extent; c++)
                {
                    if (c != column)
                    {
                        result._data[index] = _data[r * _extent + c];
                        index++;
                    }
                }
            }
        }

        return result;
    }

    /// <summary>
    /// This method returns the minor for the matrix at the given row and column.
    /// </summary>
    /// <param name="row">The row to determine the minor for.</param>
    /// <param name="column">The column to determine the minor for.</param>
    /// <returns>The minor for the matrix at the given entry.</returns>
    public double GetMinor(int row, int column)
    {
        return GetSubMatrix(row, column).Determinant;
    }

    /// <summary>
    /// This method returns the cofactor for the indicated row and column.
    /// </summary>
    /// <param name="row">The row to get the cofactor for.</param>
    /// <param name="column">The column to get the cofactor for.</param>
    /// <returns>The cofactor for the matrix at the given entry.</returns>
    public double GetCofactor(int row, int column)
    {
        double minor = GetMinor(row, column);

        return (row + column) % 2 == 0 ? minor : -minor;
    }

    /// <summary>
    /// This method produces the inverse of this matrix (if possible).
    /// </summary>
    /// <returns>The matrix that is the inverse of this one.</returns>
    public Matrix Invert()
    {
        Matrix result = new (_extent);
        double determinant = Determinant;

        for (int row = 0; row < _extent; row++)
        {
            for (int column = 0; column < _extent; column++)
            {
                double cofactor = GetCofactor(row, column) / determinant;

                result._data[column * _extent + row] = cofactor;
            }
        }

        return result;
    }

    /// <summary>
    /// This method is used to transform a ray by applying this matrix to it.
    /// </summary>
    /// <param name="ray">The ray to transform.</param>
    /// <returns>The transformed ray.</returns>
    public Ray Transform(Ray ray)
    {
        return new Ray(ray.Origin * this, ray.Direction * this);
    }

    /// <summary>
    /// This method returns whether this matrix matches the given one.  This will be
    /// true if all values in the matrices match.  Entries are compared for equivalence
    /// based on the difference being within a given tolerance range.
    /// </summary>
    /// <param name="other">The matrix to compare to.</param>
    /// <returns><c>true</c>if this matrix matches the given one.</returns>
    public bool Matches(Matrix other)
    {
        if (_data.Length != other._data.Length)
            return false;

        // ReSharper disable once LoopCanBeConvertedToQuery
        for (int index = 0; index < _data.Length; index++)
        {
            if (!_data[index].Near(other._data[index]))
                return false;
        }

        return true;
    }

    /// <summary>
    /// This method produces a string representation for the matrix.  It is intended for
    /// use in debugging so is very simplistic.
    /// </summary>
    /// <returns>A descriptive string that represents this color.</returns>
    public override string ToString()
    {
        List<List<string>> columns = [];
        List<int> columnWidths = [];

        for (int column = 0; column < _extent; column++)
        {
            List<string> data = [];
            int width = 0;

            for (int row = 0; row < _extent; row++)
            {
                int index = row * _extent + column;
                string text = $"{_data[index]}";

                width = Math.Max(width, text.Length);

                data.Add(text);
            }

            columns.Add(data);
            columnWidths.Add(width);
        }

        StringBuilder builder = new ();

        for (int row = 0; row < _extent; row++)
        {
            builder.Append("| ");

            for (int column = 0; column < _extent; column++)
            {
                string text = columns[column][row];
                int width = columnWidths[column] + 1;

                builder.Append(text.PadLeft(width));
            }

            builder.Append(" |\n");
        }

        return builder.ToString()[..^1];
    }

    // ---------
    // Operators
    // ---------

    /// <summary>
    /// This method is used to multiply two matrices together.  It is assumed that both
    /// are 4x4 matrices.
    /// </summary>
    /// <param name="left">The left matrix to multiply.</param>
    /// <param name="right">The right matrix to multiply by.</param>
    /// <returns>The new vector.</returns>
    public static Matrix operator *(Matrix left, Matrix right)
    {
        Matrix result = new (4);

        for (int row = 0; row < 4; row++)
        {
            for (int column = 0; column < 4; column++)
            {
                int index = row * 4 + column;

                result._data[index] =
                    left.Entry(row, 0) * right.Entry(0, column) +
                    left.Entry(row, 1) * right.Entry(1, column) +
                    left.Entry(row, 2) * right.Entry(2, column) +
                    left.Entry(row, 3) * right.Entry(3, column);
            }
        }

        return result;
    }

    /// <summary>
    /// This method is used to multiply a matrix by a point.  It is assumed that
    /// the matrix is 4x4.
    /// </summary>
    /// <param name="left">The matrix to multiply.</param>
    /// <param name="right">The point to multiply by.</param>
    /// <returns>The new point.</returns>
    public static Point operator *(Matrix left, Point right)
    {
        double x = left.Entry(0, 0) * right.X +
                   left.Entry(0, 1) * right.Y +
                   left.Entry(0, 2) * right.Z +
                   left.Entry(0, 3) * right.W;
        double y = left.Entry(1, 0) * right.X +
                   left.Entry(1, 1) * right.Y +
                   left.Entry(1, 2) * right.Z +
                   left.Entry(1, 3) * right.W;
        double z = left.Entry(2, 0) * right.X +
                   left.Entry(2, 1) * right.Y +
                   left.Entry(2, 2) * right.Z +
                   left.Entry(2, 3) * right.W;
        double w = left.Entry(3, 0) * right.X +
                   left.Entry(3, 1) * right.Y +
                   left.Entry(3, 2) * right.Z +
                   left.Entry(3, 3) * right.W;

        return new Point(x, y, z, w);
    }

    /// <summary>
    /// This method is used to multiply a matrix by a point.  It is assumed that
    /// the matrix is 4x4.
    /// </summary>
    /// <param name="left">The point to multiply.</param>
    /// <param name="right">The matrix to multiply to.</param>
    /// <returns>The new point.</returns>
    public static Point operator *(Point left, Matrix right)
    {
        return right * left;
    }

    /// <summary>
    /// This method is used to multiply a matrix by a vector.  It is assumed that
    /// the matrix is 4x4.
    /// </summary>
    /// <param name="left">The matrix to multiply.</param>
    /// <param name="right">The vector to multiply by.</param>
    /// <returns>The new vector.</returns>
    public static Vector operator *(Matrix left, Vector right)
    {
        double x = left.Entry(0, 0) * right.X +
                   left.Entry(0, 1) * right.Y +
                   left.Entry(0, 2) * right.Z +
                   left.Entry(0, 3) * right.W;
        double y = left.Entry(1, 0) * right.X +
                   left.Entry(1, 1) * right.Y +
                   left.Entry(1, 2) * right.Z +
                   left.Entry(1, 3) * right.W;
        double z = left.Entry(2, 0) * right.X +
                   left.Entry(2, 1) * right.Y +
                   left.Entry(2, 2) * right.Z +
                   left.Entry(2, 3) * right.W;
        double w = left.Entry(3, 0) * right.X +
                   left.Entry(3, 1) * right.Y +
                   left.Entry(3, 2) * right.Z +
                   left.Entry(3, 3) * right.W;

        return new Vector(x, y, z, w);
    }

    /// <summary>
    /// This method is used to multiply a matrix by a vector.  It is assumed that
    /// the matrix is 4x4.
    /// </summary>
    /// <param name="left">The vector to multiply.</param>
    /// <param name="right">The matrix to multiply to.</param>
    /// <returns>The new vector.</returns>
    public static Vector operator *(Vector left, Matrix right)
    {
        return right * left;
    }
}

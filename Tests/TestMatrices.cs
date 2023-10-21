using RayTracer.Basics;

namespace Tests;

[TestClass]
public class TestMatrices
{
    [TestMethod]
    public void TestConstruction()
    {
        double[] data = {
            1, 2, 3, 4,
            5.5, 6.5, 7.5, 8.5,
            9, 10, 11, 12,
            13.5, 14.5, 15.5, 16.5
        };
        Matrix matrix = new (data);

        Assert.AreEqual(1, matrix.Entry(0, 0));
        Assert.AreEqual(4, matrix.Entry(0, 3));
        Assert.AreEqual(5.5, matrix.Entry(1, 0));
        Assert.AreEqual(7.5, matrix.Entry(1, 2));
        Assert.AreEqual(11, matrix.Entry(2, 2));
        Assert.AreEqual(13.5, matrix.Entry(3, 0));
        Assert.AreEqual(15.5, matrix.Entry(3, 2));

        data = new double[] { -3, 5, 1, -2 };
        matrix = new Matrix(data);

        Assert.AreEqual(-3, matrix.Entry(0, 0));
        Assert.AreEqual(5, matrix.Entry(0, 1));
        Assert.AreEqual(1, matrix.Entry(1, 0));
        Assert.AreEqual(-2, matrix.Entry(1, 1));
    }

    [TestMethod]
    public void TestMatches()
    {
        double[] data = {
            1, 2, 3, 4,
            5, 6, 7, 8,
            9, 8, 7, 6,
            5, 4, 3, 2
        };
        Matrix matrix1 = new (data);
        Matrix matrix2 = new (data);

        Assert.IsTrue(matrix1.Matches(matrix2));

        matrix2 = new Matrix(new double[]
        {
            2, 3, 4, 5,
            6, 7, 8, 9,
            8, 7, 6, 5,
            4, 3, 2, 1
        });

        Assert.IsFalse(matrix1.Matches(matrix2));
    }

    [TestMethod]
    public void TestMultiplicationByMatrix()
    {
        Matrix matrix1 = new (new double[] {
            1, 2, 3, 4,
            5, 6, 7, 8,
            9, 8, 7, 6,
            5, 4, 3, 2
        });
        Matrix matrix2 = new (new double[]
        {
            -2, 1, 2, 3,
            3, 2, 1, -1,
            4, 3, 6, 5,
            1, 2, 7, 8
        });
        Matrix expected = new (new double[]
        {
            20, 22, 50, 48,
            44, 54, 114, 108,
            40, 58, 110, 102,
            16, 26, 46,42
        });

        Assert.IsTrue(expected.Matches(matrix1 * matrix2));
        Assert.IsTrue(matrix1.Matches(matrix1 * Matrix.Identity));
    }

    [TestMethod]
    public void TestMultiplicationByPoint()
    {
        Matrix matrix = new (new double[] {
            1, 2, 3, 4,
            2, 4, 4, 2,
            8, 6, 4, 1,
            0, 0, 0, 1
        });
        Matrix matrix2 = new (new double[]
        {
            -2, 1, 2, 3,
            3, 2, 1, -1,
            4, 3, 6, 5,
            1, 2, 7, 8
        });
        Point point = new (1, 2, 3);
        Point expected = new (18, 24, 33);

        Assert.IsTrue(expected.Matches(matrix * point));
        Assert.IsTrue(expected.Matches(point * matrix));

        point = new Point(1, 2, 3, 4);

        Assert.IsTrue(point.Matches(point * Matrix.Identity));
    }

    [TestMethod]
    public void TestMultiplicationByVector()
    {
        Matrix matrix = new (new double[] {
            1, 2, 3, 4,
            2, 4, 4, 2,
            8, 6, 4, 1,
            0, 0, 0, 1
        });
        Matrix matrix2 = new (new double[]
        {
            -2, 1, 2, 3,
            3, 2, 1, -1,
            4, 3, 6, 5,
            1, 2, 7, 8
        });
        Vector vector = new (1, 2, 3, 1);
        Vector expected = new (18, 24, 33, 1);

        Assert.IsTrue(expected.Matches(matrix * vector));
        Assert.IsTrue(expected.Matches(vector * matrix));

        vector = new Vector(1, 2, 3, 4);

        Assert.IsTrue(vector.Matches(vector * Matrix.Identity));
    }

    [TestMethod]
    public void TestTranspose()
    {
        Matrix matrix = new (new double[]
        {
            0, 9, 3, 0,
            9, 8, 0, 8,
            1, 8, 5, 3,
            0, 0, 5, 8
        });
        Matrix expected = new (new double[]
        {
            0, 9, 1, 0,
            9, 8, 8, 0,
            3, 0, 5, 5,
            0, 8, 3, 8
        });

        Assert.IsTrue(expected.Matches(matrix.Transpose()));
        Assert.IsTrue(Matrix.Identity.Matches(Matrix.Identity.Transpose()));
    }

    [TestMethod]
    public void TestDeterminant()
    {
        Matrix matrix = new (new double[]
        {
            1, 5,
            -3, 2
        });

        Assert.AreEqual(17, matrix.Determinant);

        matrix = new Matrix(new double[]
        {
            1, 2, 6,
            -5, 8, -4,
            2, 6, 4
        });

        Assert.AreEqual(56, matrix.GetCofactor(0, 0));
        Assert.AreEqual(12, matrix.GetCofactor(0, 1));
        Assert.AreEqual(-46, matrix.GetCofactor(0, 2));
        Assert.AreEqual(-196, matrix.Determinant);

        matrix = new Matrix(new double[]
        {
            -2, -8, 3, 5,
            -3, 1, 7, 3,
            1, 2, -9, 6,
            -6, 7, 7, -9
        });

        Assert.AreEqual(690, matrix.GetCofactor(0, 0));
        Assert.AreEqual(447, matrix.GetCofactor(0, 1));
        Assert.AreEqual(210, matrix.GetCofactor(0, 2));
        Assert.AreEqual(51, matrix.GetCofactor(0, 3));
        Assert.AreEqual(-4071, matrix.Determinant);
    }

    [TestMethod]
    public void TestSubMatrix()
    {
        Matrix matrix = new (new double[]
        {
            1, 5, 0,
            -3, 2, 7,
            0, 6, -3
        });
        Matrix expected = new (new double[]
        {
            -3, 2,
            0, 6
        });

        Assert.IsTrue(expected.Matches(matrix.GetSubMatrix(0, 2)));

        matrix = new Matrix(new double[]
        {
            -6, 1, 1, 6,
            -8, 5, 8, 6,
            -1, 0, 8, 2,
            -7, 1, -1, 1
        });
        expected = new Matrix(new double[]
        {
            -6, 1, 6,
            -8, 8, 6,
            -7, -1, 1
        });

        Assert.IsTrue(expected.Matches(matrix.GetSubMatrix(2, 1)));
    }

    [TestMethod]
    public void TestMinor()
    {
        Matrix matrix = new (new double[]
        {
            3, 5, 0,
            2, -1, -7,
            6, -1, 5
        });

        Assert.AreEqual(25, matrix.GetMinor(1, 0));
    }

    [TestMethod]
    public void TestCofactor()
    {
        Matrix matrix = new (new double[]
        {
            3, 5, 0,
            2, -1, -7,
            6, -1, 5
        });

        Assert.AreEqual(-12, matrix.GetMinor(0, 0));
        Assert.AreEqual(-12, matrix.GetCofactor(0, 0));

        Assert.AreEqual(25, matrix.GetMinor(1, 0));
        Assert.AreEqual(-25, matrix.GetCofactor(1, 0));
    }

    [TestMethod]
    public void TestCanInvert()
    {
        Matrix matrix = new (new double[]
        {
            6, 4, 4, 4,
            5, 5, 7, 6,
            4, -9, 3, -7,
            9, 1, 7, -6
        });

        Assert.AreEqual(-2120, matrix.Determinant);
        Assert.IsTrue(matrix.CanInvert);

        matrix = new Matrix(new double[]
        {
            -4, 2, -2, -3,
            9, 6, 2, 6,
            0, -5, 1, -5,
            0, 0, 0, 0
        });

        Assert.AreEqual(0, matrix.Determinant);
        Assert.IsFalse(matrix.CanInvert);
    }

    [TestMethod]
    public void TestInvert()
    {
        Matrix matrix = new (new double[]
        {
            -5, 2, 6, -8,
            1, -5, 1, 8,
            7, 7, -6, -7,
            1, -3, 7, 4
        });
        Matrix expected = new (new double[]
        {
             0.21805,  0.45113,  0.24060, -0.04511,
            -0.80827, -1.45677, -0.44361,  0.52068,
            -0.07895, -0.22368, -0.05263,  0.19737,
            -0.52256, -0.81391, -0.30075,  0.30639
        });

        Assert.AreEqual(532, matrix.Determinant);
        Assert.AreEqual(-160, matrix.GetCofactor(2, 3));
        Assert.AreEqual(105, matrix.GetCofactor(3, 2));

        Matrix inverse = matrix.Invert();

        Assert.AreEqual(-160.0 / 532.0, inverse.Entry(3, 2));
        Assert.AreEqual(105.0 / 532.0, inverse.Entry(2, 3));
        Assert.IsTrue(expected.Matches(inverse));

        matrix = new Matrix(new double[]
        {
            8, -5, 9, 2,
            7, 5, 6, 1,
            -6, 0, 9, 6,
            -3, 0, -9, -4
        });
        expected = new (new double[]
        {
            -0.15385, -0.15385, -0.28205, -0.53846,
            -0.07692,  0.12308,  0.02564,  0.03077,
             0.35897,  0.35897,  0.43590,  0.92308,
            -0.69231, -0.69231, -0.76923, -1.92308
        });

        Assert.IsTrue(expected.Matches(matrix.Invert()));

        matrix = new Matrix(new double[]
        {
            9, 3, 0, 9,
            -5, -2, -6, -3,
            -4, 9, 6, 4,
            -7, 6, 6, 2
        });
        expected = new Matrix(new double[]
        {
            -0.04074, -0.07778,  0.14444, -0.22222,
            -0.07778,  0.03333,  0.36667, -0.33333,
            -0.02901, -0.14630, -0.10926,  0.12963,
             0.17778,  0.06667, -0.26667,  0.33333
        });

        Assert.IsTrue(expected.Matches(matrix.Invert()));

        matrix = new Matrix(new double[]
        {
            3, -9, 7, 3,
            3, -8, 2, -9,
            -4, 4, 4, 1,
            -6, 5, -1, 1
        });

        Matrix other = new Matrix(new double[]
        {
            8, 2, 2, 2,
            3, -1, 7, 0,
            7, 0, 5, 4,
            6, -2, 0, 5
        });
        Matrix product = matrix * other;
        Matrix reverted = product * other.Invert();

        Assert.IsTrue(matrix.Matches(reverted));
    }

    [TestMethod]
    public void TestTransformRay()
    {
        Point point = new (1, 2, 3);
        Vector vector = new (0, 1, 0);
        Ray ray = new (point, vector);
        Matrix transform = Transforms.Translate(3, 4, 5);
        Ray transformed = transform.Transform(ray);

        Assert.IsTrue(new Point(4, 6, 8).Matches(transformed.Origin));
        Assert.IsTrue(vector.Matches(transformed.Direction));

        transform = Transforms.Scale(2, 3, 4);
        transformed = transform.Transform(ray);

        Assert.IsTrue(new Point(2, 6, 12).Matches(transformed.Origin));
        Assert.IsTrue(new Vector(0, 3, 0).Matches(transformed.Direction));
    }
}

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
        Matrix matrix = new (
        [
            -5, 2, 6, -8,
            1, -5, 1, 8,
            7, 7, -6, -7,
            1, -3, 7, 4
        ]);
        Matrix expected = new (
        [
            0.218045,  0.451128,  0.240602, -0.045113,
            -0.808271, -1.456767, -0.443609,  0.520677,
            -0.078947, -0.223684, -0.052632,  0.197368,
            -0.522556, -0.813910, -0.300752,  0.306391
        ]);

        Assert.AreEqual(532, matrix.Determinant);
        Assert.AreEqual(-160, matrix.GetCofactor(2, 3));
        Assert.AreEqual(105, matrix.GetCofactor(3, 2));

        Matrix inverse = matrix.Invert();

        Assert.AreEqual(-160.0 / 532.0, inverse.Entry(3, 2));
        Assert.AreEqual(105.0 / 532.0, inverse.Entry(2, 3));
        Assert.IsTrue(expected.Matches(inverse));

        matrix = new Matrix(
        [
            8, -5, 9, 2,
            7, 5, 6, 1,
            -6, 0, 9, 6,
            -3, 0, -9, -4
        ]);
        expected = new Matrix(
        [
            -0.153846, -0.153846, -0.282051, -0.538462,
            -0.076923,  0.123077,  0.025641,  0.030769,
             0.358974,  0.358974,  0.435897,  0.923077,
            -0.692308, -0.692308, -0.769231, -1.923077
        ]);

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
            -0.040740, -0.077778,  0.144444, -0.222222,
            -0.077778,  0.033333,  0.366667, -0.333333,
            -0.029012, -0.146296, -0.109259,  0.129630,
             0.177778,  0.066667, -0.266667,  0.333333
        });

        Assert.IsTrue(expected.Matches(matrix.Invert()));

        matrix = new Matrix(
        [
            3, -9, 7, 3,
            3, -8, 2, -9,
            -4, 4, 4, 1,
            -6, 5, -1, 1
        ]);

        Matrix other = new Matrix(
        [
            8, 2, 2, 2,
            3, -1, 7, 0,
            7, 0, 5, 4,
            6, -2, 0, 5
        ]);
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

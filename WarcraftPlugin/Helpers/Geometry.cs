using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Utils;
using g3;
using MIConvexHull;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace WarcraftPlugin.Helpers
{
    public static class Geometry
    {
        public static Box3d CreateBoxAroundPoint(Vector point, double sizeX, double sizeY, double heightZ)
        {
            Vector3d min = new(point.X - sizeX / 2, point.Y - sizeY / 2, point.Z - heightZ / 2);
            Vector3d max = new(point.X + sizeX / 2, point.Y + sizeY / 2, point.Z + heightZ / 2);
            return new Box3d(new AxisAlignedBox3d(min, max));
        }

        public static List<Vector3d> CreateSphereAroundPoint(Vector point, double radius, int numLatitudeSegments = 10, int numLongitudeSegments = 10)
        {
            var vertices = new List<Vector3d>();

            // Generate vertices
            for (int lat = 0; lat <= numLatitudeSegments; lat++)
            {
                double theta = lat * Math.PI / numLatitudeSegments;
                double sinTheta = Math.Sin(theta);
                double cosTheta = Math.Cos(theta);

                for (int lon = 0; lon <= numLongitudeSegments; lon++)
                {
                    double phi = lon * 2 * Math.PI / numLongitudeSegments;
                    double sinPhi = Math.Sin(phi);
                    double cosPhi = Math.Cos(phi);

                    double x = cosPhi * sinTheta;
                    double y = cosTheta;
                    double z = sinPhi * sinTheta;

                    vertices.Add(new Vector3d(x * radius, y * radius, z * radius) + point.ToVector3d());
                }
            }

            return vertices;
        }

        public static Vector3d ToVector3d(this Vector vector)
        {
            return new Vector3d(vector.X, vector.Y, vector.Z);
        }

        public static Vector ToVector(this Vector3d vector)
        {
            return new Vector((float?)vector.x, (float?)vector.y, (float?)vector.z);
        }

        public static void DrawVertices(IEnumerable<Vector3d> vertices, Color? color = null, float duration = 5, float width = 0.1f)
        {
            if (!vertices.Any())
            {
                Console.WriteLine("No vertices to draw");
                return;
            }

            var convexHull = ConvexHull.Create(vertices.Select(vertex => new double[] { vertex.x, vertex.y, vertex.z }).ToArray());
            int i = 0;
            float frequency = 0.1f;
            foreach (var face in convexHull.Result.Faces)
            {
                var facecolor = color ?? Color.FromArgb(255, (int)(Math.Sin(frequency * i + 0) * 127 + 128), (int)(Math.Sin(frequency * i + 2) * 127 + 128), (int)(Math.Sin(frequency * i + 4) * 127 + 128));
                Warcraft.DrawLaserBetween(new Vector((float)face.Vertices[0].Position[0], (float)face.Vertices[0].Position[1], (float)face.Vertices[0].Position[2]), new Vector((float)face.Vertices[1].Position[0], (float)face.Vertices[1].Position[1], (float)face.Vertices[1].Position[2]), facecolor, duration, width);
                Warcraft.DrawLaserBetween(new Vector((float)face.Vertices[1].Position[0], (float)face.Vertices[1].Position[1], (float)face.Vertices[1].Position[2]), new Vector((float)face.Vertices[2].Position[0], (float)face.Vertices[2].Position[1], (float)face.Vertices[2].Position[2]), facecolor, duration, width);
                Warcraft.DrawLaserBetween(new Vector((float)face.Vertices[2].Position[0], (float)face.Vertices[2].Position[1], (float)face.Vertices[2].Position[2]), new Vector((float)face.Vertices[0].Position[0], (float)face.Vertices[0].Position[1], (float)face.Vertices[0].Position[2]), facecolor, duration, width);

                i++;
            }
        }

        public static Vector GetRandomPoint(this Box3d box)
        {
            var random = Random.Shared;
            // Generate random coordinates within the range [-1, 1]
            double x = (2 * random.NextDouble() - 1) * box.Extent.x;
            double y = (2 * random.NextDouble() - 1) * box.Extent.y;
            double z = (2 * random.NextDouble() - 1) * box.Extent.z;

            // Transform coordinates to be relative to the box's center
            var randomPoint = box.Center + x * box.AxisX + y * box.AxisY + z * box.AxisZ;

            return randomPoint.ToVector();
        }

        public static Box3d ToBox(this CCollisionProperty collision, Vector worldPosition)
        {
            Vector worldCenter = worldPosition.With().Add(z: collision.Mins.Z + (collision.Maxs.Z - collision.Mins.Z) / 2);
            return CreateBoxAroundPoint(worldCenter, collision.Maxs.X * 2, collision.Maxs.Y * 2, collision.Maxs.Z);
        }

        public static Vector Add(this Vector vector, float x = 0, float y = 0, float z = 0)
        {
            vector.X += x;
            vector.Y += y;
            vector.Z += z;
            return vector;
        }

        public static Vector Multiply(this Vector vector, float x = 1, float y = 1, float z = 1)
        {
            vector.X *= x;
            vector.Y *= y;
            vector.Z *= z;
            return vector;
        }

        public static bool IsEqual(this Vector vector1, Vector vector2, bool floor = false)
        {
            if (floor)
            {
                return Math.Floor(vector1.X) == Math.Floor(vector2.X) &&
                       Math.Floor(vector1.Y) == Math.Floor(vector2.Y) &&
                       Math.Floor(vector1.Z) == Math.Floor(vector2.Z);
            }
            else
            {
                return vector1.X == vector2.X && vector1.Y == vector2.Y && vector1.Z == vector2.Z;
            }
        }
    }
}

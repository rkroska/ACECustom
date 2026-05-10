using ACE.Entity;
using System;
using System.Collections.Generic;

namespace ACE.Server.Command.Handlers
{
    public static class PatternGenerator
    {
        public static List<Position> GeneratePattern(Position centerPos, int count, string pattern, float radius)
        {
            if (count <= 0) return new List<Position>();

            switch (pattern?.ToLower())
            {
                case "circle":    return GenerateCircle(centerPos, count, radius);
                case "triangle":  return GenerateOutline(centerPos, count, radius, TrianglePoints);
                case "rectangle": return GenerateOutline(centerPos, count, radius, RectPoints(1.0f, 0.6f));
                case "square":    return GenerateOutline(centerPos, count, radius, RectPoints(1.0f, 1.0f));
                case "line":      return GenerateLine(centerPos, count, radius);
                case "star":      return GenerateOutline(centerPos, count, radius, StarPoints);
                case "arrow":     return GenerateOutline(centerPos, count, radius, ArrowPoints);
                case "cross":     return GenerateOutline(centerPos, count, radius, CrossPoints);
                case "diamond":   return GenerateOutline(centerPos, count, radius, DiamondPoints);
                case "crown":     return GenerateOutline(centerPos, count, radius, CrownPoints);
                case "heart":     return GenerateOutline(centerPos, count, radius, HeartPoints);
                case "boot":      return GenerateOutline(centerPos, count, radius, BootPoints);
                case "sword":     return GenerateOutline(centerPos, count, radius, SwordPoints);
                case "spiral":    return GenerateSpiral(centerPos, count, radius);
                case "penis":     return GenerateOutline(centerPos, count, radius, PenisPoints);
                default:
                    return new List<Position> { centerPos };
            }
        }

        // ── Core generators ─────────────────────────────────────────────────────

        private static List<Position> GenerateCircle(Position centerPos, int count, float radius)
        {
            var positions = new List<Position>();
            var step = 360.0 / count;
            for (int i = 0; i < count; i++)
                positions.Add(PolarOffset(centerPos, radius, i * step));
            return positions;
        }

        private static List<Position> GenerateLine(Position centerPos, int count, float radius)
        {
            var positions = new List<Position>();
            var halfLen = (count - 1) * radius / 2.0;
            for (int i = 0; i < count; i++)
                positions.Add(XYOffset(centerPos, 0f, (float)(-halfLen + i * radius)));
            return positions;
        }

        private static List<Position> GenerateSpiral(Position centerPos, int count, float radius)
        {
            var positions = new List<Position>();
            float inner = radius * 0.1f;
            float turns = 3f;
            int n = Math.Max(count - 1, 1);
            for (int i = 0; i < count; i++)
            {
                var t = (double)i / n;
                var angle = t * turns * 360.0;
                var r = inner + (radius - inner) * (float)t;
                positions.Add(PolarOffset(centerPos, r, angle));
            }
            return positions;
        }

        /// <summary>Distribute mobs evenly along the perimeter of a closed polygon.</summary>
        private static List<Position> GenerateOutline(Position centerPos, int count, float radius, (float x, float y)[] pts)
        {
            int n = pts.Length;

            // Cumulative arc lengths
            var cumLen = new double[n + 1];
            for (int i = 0; i < n; i++)
            {
                var a = pts[i];
                var b = pts[(i + 1) % n];
                var dx = b.x - a.x;
                var dy = b.y - a.y;
                cumLen[i + 1] = cumLen[i] + Math.Sqrt(dx * dx + dy * dy);
            }
            double total = cumLen[n];

            var positions = new List<Position>();
            for (int i = 0; i < count; i++)
            {
                var target = ((double)i / count) * total;
                for (int e = 0; e < n; e++)
                {
                    if (target < cumLen[e + 1] || e == n - 1)
                    {
                        var segLen = cumLen[e + 1] - cumLen[e];
                        var t = segLen > 0 ? Math.Min((target - cumLen[e]) / segLen, 1.0) : 0;
                        var from = pts[e];
                        var to = pts[(e + 1) % n];
                        positions.Add(XYOffset(centerPos,
                            (float)(from.x * radius + t * (to.x - from.x) * radius),
                            (float)(from.y * radius + t * (to.y - from.y) * radius)));
                        break;
                    }
                }
            }
            return positions;
        }

        // ── Position math ────────────────────────────────────────────────────────

        /// <summary>Polar offset — 0° = forward, clockwise positive.</summary>
        private static Position PolarOffset(Position center, float distance, double angleDegs)
        {
            var rad = angleDegs * Math.PI / 180.0;
            return XYOffset(center, (float)(distance * Math.Sin(rad)), (float)(distance * Math.Cos(rad)));
        }

        /// <summary>Apply a local-space offset (X = right, Y = forward) rotated to player heading.</summary>
        private static Position XYOffset(Position center, float localX, float localY)
        {
            // Heading from quaternion — same formula as Position.InFrontOf
            float qw = center.RotationW;
            float qz = center.RotationZ;
            var h = Math.Atan2(2 * qw * qz, 1 - 2 * qz * qz); // radians

            // Forward = (−sin h, cos h),  Right = (cos h, sin h)
            var worldX = localX * Math.Cos(h) - localY * Math.Sin(h);
            var worldY = localX * Math.Sin(h) + localY * Math.Cos(h);

            var pos = new Position(center);
            pos.PositionX = center.PositionX + (float)worldX;
            pos.PositionY = center.PositionY + (float)worldY;
            pos.PositionZ = center.PositionZ + 0.05f;
            pos.SetLandblock();
            pos.SetLandCell();
            return pos;
        }

        // ── Shape outlines (normalised: max extent = 1.0) ────────────────────────

        private static (float x, float y)[] RectPoints(float w, float h) => new[]
        {
            (-w,  h), ( w,  h),
            ( w, -h), (-w, -h),
        };

        private static readonly (float x, float y)[] TrianglePoints =
        {
            (0f, 1f),
            ( 0.866f, -0.5f),
            (-0.866f, -0.5f),
        };

        // 5-point star — 10 alternating outer/inner vertices
        private static readonly (float x, float y)[] StarPoints = BuildStar();
        private static (float x, float y)[] BuildStar()
        {
            var pts = new (float x, float y)[10];
            for (int i = 0; i < 10; i++)
            {
                var ang = i * 36.0 * Math.PI / 180.0;
                var r = i % 2 == 0 ? 1.0 : 0.38;
                pts[i] = ((float)(r * Math.Sin(ang)), (float)(r * Math.Cos(ang)));
            }
            return pts;
        }

        private static readonly (float x, float y)[] ArrowPoints =
        {
            // Tip forward, wings back
            ( 0f,    1f   ),
            ( 0.6f,  0.1f ),
            ( 0.25f, 0.1f ),
            ( 0.25f,-1f   ),
            (-0.25f,-1f   ),
            (-0.25f, 0.1f ),
            (-0.6f,  0.1f ),
        };

        private static readonly (float x, float y)[] CrossPoints =
        {
            (-0.2f,  1f  ), ( 0.2f,  1f  ),
            ( 0.2f,  0.2f), ( 1f,    0.2f),
            ( 1f,   -0.2f), ( 0.2f, -0.2f),
            ( 0.2f, -1f  ), (-0.2f, -1f  ),
            (-0.2f, -0.2f), (-1f,   -0.2f),
            (-1f,    0.2f), (-0.2f,  0.2f),
        };

        private static readonly (float x, float y)[] DiamondPoints =
        {
            (0f,  1f), (1f,  0f),
            (0f, -1f), (-1f, 0f),
        };

        private static readonly (float x, float y)[] CrownPoints =
        {
            (-1f, -1f), (-1f, -0.1f),
            (-0.6f, 1f),              // left spike
            (-0.2f,  0f),
            ( 0f,   1f),              // centre spike
            ( 0.2f,  0f),
            ( 0.6f, 1f),              // right spike
            ( 1f,  -0.1f), (1f, -1f),
        };

        private static readonly (float x, float y)[] HeartPoints =
        {
            ( 0f,   -1f   ),
            (-0.5f, -0.3f ),
            (-1f,    0.2f ),
            (-0.8f,  0.8f ),
            (-0.3f,  1f   ),
            ( 0f,    0.6f ),   // top-centre valley
            ( 0.3f,  1f   ),
            ( 0.8f,  0.8f ),
            ( 1f,    0.2f ),
            ( 0.5f, -0.3f ),
        };

        // Side-profile boot: toe = +X (player right), shaft top = +Y (player forward)
        // Traced clockwise from toe-front-bottom. More points = smoother curves.
        private static readonly (float x, float y)[] BootPoints =
        {
            // Toe — rounded front
            ( 1.00f, -0.30f), // toe tip bottom
            ( 1.00f,  0.00f), // toe tip
            ( 0.90f,  0.18f), // toe upper
            // Top of foot / instep — slopes back toward ankle
            ( 0.40f,  0.28f), // instep
            // Ankle — pinches in before shaft flares
            ( 0.22f,  0.48f), // ankle front
            // Shaft — rises up, straight front
            ( 0.30f,  0.70f), // shaft front lower
            ( 0.30f,  1.00f), // shaft top-front
            // Shaft top — narrow opening
            (-0.30f,  1.00f), // shaft top-back
            // Shaft back — straight, slight taper toward ankle
            (-0.32f,  0.68f), // shaft back lower
            (-0.42f,  0.44f), // ankle back
            // Heel bump — the distinctive boot silhouette feature
            (-0.60f,  0.20f), // heel back upper
            (-0.92f,  0.05f), // heel back
            (-1.00f, -0.18f), // heel back-bottom
            // Heel sole — rounded corner
            (-0.88f, -0.48f), // heel bottom-back
            (-0.58f, -0.62f), // heel bottom
            // Sole — runs flat toward toe
            ( 0.15f, -0.62f), // sole mid
            ( 0.72f, -0.50f), // sole front
            // Back to toe bottom
            ( 0.95f, -0.35f), // sole near toe
        };

        // Sword: blade tip = +Y (forward), pommel = −Y
        private static readonly (float x, float y)[] SwordPoints =
        {
            ( 0f,    1f   ), // blade tip
            ( 0.06f, 0.2f ), // blade right
            ( 0.5f,  0.08f), // crossguard right end
            ( 0.5f, -0.06f), // crossguard right bottom
            ( 0.1f, -0.18f), // grip right top
            ( 0.12f,-0.65f), // grip right bottom
            ( 0.26f,-0.78f), // pommel right shoulder
            ( 0.16f,-1f   ), // pommel bottom-right
            (-0.16f,-1f   ), // pommel bottom-left
            (-0.26f,-0.78f), // pommel left shoulder
            (-0.12f,-0.65f), // grip left bottom
            (-0.1f, -0.18f), // grip left top
            (-0.5f, -0.06f), // crossguard left bottom
            (-0.5f,  0.08f), // crossguard left end
            (-0.06f, 0.2f ), // blade left
        };

        // The fun one — outline clockwise from shaft-base-right
        private static readonly (float x, float y)[] PenisPoints =
        {
            // Shaft right, going up
            ( 0.25f, -0.1f ),
            ( 0.25f,  0.42f),
            // Glans right
            ( 0.45f,  0.52f),
            ( 0.52f,  0.68f),
            ( 0.42f,  0.87f),
            ( 0.18f,  0.98f),
            // Tip
            ( 0f,     1f   ),
            // Glans left
            (-0.18f,  0.98f),
            (-0.42f,  0.87f),
            (-0.52f,  0.68f),
            (-0.45f,  0.52f),
            // Shaft left, going down
            (-0.25f,  0.42f),
            (-0.25f, -0.1f ),
            // Left testicle
            (-0.5f,  -0.1f ),
            (-0.78f, -0.22f),
            (-0.85f, -0.48f),
            (-0.68f, -0.72f),
            (-0.35f, -0.8f ),
            (-0.12f, -0.6f ),
            // Taint
            ( 0f,    -0.5f ),
            ( 0.12f, -0.6f ),
            // Right testicle
            ( 0.35f, -0.8f ),
            ( 0.68f, -0.72f),
            ( 0.85f, -0.48f),
            ( 0.78f, -0.22f),
            ( 0.5f,  -0.1f ),
        };
    }
}

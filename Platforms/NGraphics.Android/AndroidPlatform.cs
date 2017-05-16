using System;
using System.Collections.Generic;
using Android.Graphics;
using Android.Text;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace NGraphics
{
    public class AndroidPlatform : IPlatform
    {
        public string Name { get { return "Android"; } }

        public Task<Stream> OpenFileStreamForWritingAsync(string path)
        {
            return Task.FromResult((Stream)new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read));
        }

        public IImageCanvas CreateImageCanvas(Size size, double scale = 1.0, bool transparency = true)
        {
            var pixelWidth = (int)Math.Ceiling(size.Width * scale);
            var pixelHeight = (int)Math.Ceiling(size.Height * scale);
            var bitmap = Bitmap.CreateBitmap(pixelWidth, pixelHeight, Bitmap.Config.Argb8888);
            if (!transparency)
            {
                bitmap.EraseColor(Colors.Black.Argb);
            }
            return new BitmapCanvas(bitmap, scale);
        }

        public IImage LoadImage(Stream stream)
        {
            var bitmap = BitmapFactory.DecodeStream(stream);
            return new BitmapImage(bitmap);
        }

        public IImage LoadImage(string path)
        {
            var bitmap = BitmapFactory.DecodeFile(path);
            return new BitmapImage(bitmap);
        }

        public IImage CreateImage(Color[] colors, int width, double scale = 1.0)
        {
            var pixelWidth = width;
            var pixelHeight = colors.Length / width;
            var acolors = new int[pixelWidth * pixelHeight];
            for (var i = 0; i < colors.Length; i++)
            {
                acolors[i] = colors[i].Argb;
            }
            var bitmap = Bitmap.CreateBitmap(acolors, pixelWidth, pixelHeight, Bitmap.Config.Argb8888);
            return new BitmapImage(bitmap, scale);
        }

        public static TextPaint GlobalGetFontPaint(Font font, TextAlignment alignment)
        {
            var paint = new TextPaint(PaintFlags.AntiAlias);
            paint.TextAlign = Paint.Align.Left;
            if (alignment == TextAlignment.Center)
                paint.TextAlign = Paint.Align.Center;
            else if (alignment == TextAlignment.Right)
                paint.TextAlign = Paint.Align.Right;

            paint.TextSize = (float)font.Size;
            var typeface = Typeface.Create(font.Family, TypefaceStyle.Normal);
            paint.SetTypeface(typeface);

            return paint;
        }

        public static TextMetrics GlobalMeasureText(string text, Font font)
        {
            var paint = GlobalGetFontPaint(font, TextAlignment.Left);
            var w = paint.MeasureText(text);
            var fm = paint.GetFontMetrics();
            return new TextMetrics
            {
                Width = w,
                Ascent = -fm.Ascent,
                Descent = fm.Descent
            };
        }

        public TextMetrics MeasureText(string text, Font font)
        {
            return GlobalMeasureText(text, font);
        }
    }

    public class BitmapImage : IImage
    {
        readonly Bitmap bitmap;
        //		readonly double scale;

        public Bitmap Bitmap
        {
            get
            {
                return bitmap;
            }
        }

        public BitmapImage(Bitmap bitmap, double scale = 1.0)
        {
            this.bitmap = bitmap;
            //			this.scale = scale;
        }

        public void SaveAsPng(string path)
        {
            using (var f = System.IO.File.OpenWrite(path))
            {
                bitmap.Compress(Bitmap.CompressFormat.Png, 100, f);
            }
        }

        public void SaveAsPng(Stream stream)
        {
            bitmap.Compress(Bitmap.CompressFormat.Png, 100, stream);
        }

        public Size Size
        {
            get
            {
                return new Size(bitmap.Width, bitmap.Height);
            }
        }

        public double Scale
        {
            get
            {
                return 1;
            }
        }
    }

    public class BitmapCanvas : CanvasCanvas, IImageCanvas
    {
        readonly Bitmap bitmap;
        readonly double scale;

        public Size Size { get { return new Size(bitmap.Width / scale, bitmap.Height / scale); } }
        public double Scale { get { return scale; } }

        public BitmapCanvas(Bitmap bitmap, double scale = 1.0)
            : base(new Canvas(bitmap))
        {
            this.bitmap = bitmap;
            this.scale = scale;

            graphics.Scale((float)scale, (float)scale);
        }

        public IImage GetImage()
        {
            return new BitmapImage(bitmap, scale);
        }
    }

    public class CanvasCanvas : ICanvas
    {
        protected readonly Canvas graphics;

        public CanvasCanvas(Canvas graphics)
        {
            this.graphics = graphics;
        }

        public void SaveState()
        {
            graphics.Save(SaveFlags.Matrix | SaveFlags.Clip);
        }
        public void Transform(Transform transform)
        {
            var t = new Matrix();
            t.SetValues(new[] {
                (float)transform.A, (float)transform.C, (float)transform.E,
                (float)transform.B, (float)transform.D, (float)transform.F,
                0, 0, 1,
            });
            graphics.Concat(t);
        }
        public void RestoreState()
        {
            graphics.Restore();
        }

        Paint GetImagePaint(double alpha)
        {
            var paint = new Paint(PaintFlags.AntiAlias);
            paint.FilterBitmap = true;
            paint.Alpha = (int)(alpha * 255);
            return paint;
        }
        Paint GetPenPaint(Pen pen)
        {
            var paint = new Paint(PaintFlags.AntiAlias);
            paint.SetStyle(Paint.Style.Stroke);
            paint.SetARGB(pen.Color.A, pen.Color.R, pen.Color.G, pen.Color.B);
            paint.StrokeWidth = (float)pen.Width;

            if (pen.DashPattern != null && pen.DashPattern.Any())
            {
                var dashPathEffect = new DashPathEffect(pen.DashPattern.ToArray(), 0);
                paint.SetPathEffect(dashPathEffect);
            }

            return paint;
        }
        Paint GetBrushPaint(Brush brush, Rect frame)
        {
            var paint = new Paint(PaintFlags.AntiAlias);
            AddBrushPaint(paint, brush, frame);
            return paint;
        }
        void AddBrushPaint(Paint paint, Brush brush, Rect bb)
        {
            paint.SetStyle(Paint.Style.Fill);

            var sb = brush as SolidBrush;
            if (sb != null)
            {
                paint.SetARGB(sb.Color.A, sb.Color.R, sb.Color.G, sb.Color.B);
                return;
            }

            var lgb = brush as LinearGradientBrush;
            if (lgb != null)
            {
                var n = lgb.Stops.Count;
                if (n >= 2)
                {
                    var locs = new float[n];
                    var comps = new int[n];
                    for (var i = 0; i < n; i++)
                    {
                        var s = lgb.Stops[i];
                        locs[i] = (float)s.Offset;
                        comps[i] = s.Color.Argb;
                    }
                    var p1 = lgb.Absolute ? lgb.Start : bb.Position + lgb.Start * bb.Size;
                    var p2 = lgb.Absolute ? lgb.End : bb.Position + lgb.End * bb.Size;
                    var lg = new LinearGradient(
                                (float)p1.X, (float)p1.Y,
                                (float)p2.X, (float)p2.Y,
                                comps,
                                locs,
                                Shader.TileMode.Clamp);
                    paint.SetShader(lg);
                }
                return;
            }

            var rgb = brush as RadialGradientBrush;
            if (rgb != null)
            {
                var n = rgb.Stops.Count;
                if (n >= 2)
                {
                    var locs = new float[n];
                    var comps = new int[n];
                    for (var i = 0; i < n; i++)
                    {
                        var s = rgb.Stops[i];
                        locs[i] = (float)s.Offset;
                        comps[i] = s.Color.Argb;
                    }
                    var p1 = rgb.GetAbsoluteCenter(bb);
                    var r = rgb.GetAbsoluteRadius(bb);
                    var rg = new RadialGradient(
                                (float)p1.X, (float)p1.Y,
                                (float)r.Max,
                                comps,
                                locs,
                                Shader.TileMode.Clamp);

                    paint.SetShader(rg);
                }
                return;
            }

            throw new NotSupportedException("Brush " + brush);
        }

        public TextMetrics MeasureText(string text, Font font)
        {
            return AndroidPlatform.GlobalMeasureText(text, font);
        }

        public void DrawText(string text, Rect frame, Font font, TextAlignment alignment = TextAlignment.Left, Pen pen = null, Brush brush = null)
        {
            if (brush == null)
                return;

            var paint = AndroidPlatform.GlobalGetFontPaint(font, alignment);
            var w = paint.MeasureText(text);
            var fm = paint.GetFontMetrics();
            var h = fm.Ascent + fm.Descent;
            var point = alignment == TextAlignment.Left
                ? frame.TopLeft
                : alignment == TextAlignment.Center
                    ? (frame.TopLeft + frame.TopRight) / 2
                    : frame.TopRight;
            var fr = new Rect(point, new Size(w, h));
            AddBrushPaint(paint, brush, fr);
            graphics.DrawText(text, (float)point.X, (float)point.Y, paint);
        }

        private BoundingBoxBuilder ComputePath(IEnumerable<PathOp> ops, out global::Android.Graphics.Path path, bool debug = false)
        {
            path = new global::Android.Graphics.Path();


            var bb = new BoundingBoxBuilder();

            Point? prevPoint = null;

            foreach (var op in ops)
            {
                var mt = op as MoveTo;
                if (mt != null)
                {
                    var p = mt.Point;
                    path.MoveTo((float)p.X, (float)p.Y);
                    bb.Add(p);
                    prevPoint = p;
                    continue;
                }
                var lt = op as LineTo;
                if (lt != null)
                {
                    var p = lt.Point;
                    path.LineTo((float)p.X, (float)p.Y);
                    bb.Add(p);
                    prevPoint = p;
                    continue;
                }
                var at = op as ArcTo;
                if (at != null)
                {
                    var p = at.Point;

                    if (!prevPoint.HasValue)
                    {
                        throw new NotSupportedException("Cannot begin path with Arc.");
                    }

                    var pp = prevPoint.Value;

                    // as necessary, scale radius to encompass points
                    // http://www.w3.org/TR/SVG/implnote.html#ArcCorrectionOutOfRangeRadii
                    {
                        // treat zero radii as a straight line
                        if (at.Radius.Width == 0 || at.Radius.Height == 0)
                        {
                            path.MoveTo((float)pp.X, (float)pp.Y);
                            path.LineTo((float)p.X, (float)p.Y);
                            bb.Add(p);
                            bb.Add(pp);
                            return bb;
                        }

                        // take absolute value of radii
                        at.Radius = new Size(
                            Math.Abs(at.Radius.Width),
                            Math.Abs(at.Radius.Height));

                        // now scale the radius up as necessary
                        var pDelta = pp - p;
                        pDelta *= 0.5;

                        var squareRadiusWidth = at.Radius.Width * at.Radius.Width;
                        var squareRadiusHeight = at.Radius.Height * at.Radius.Height;
                        var squareX = pDelta.X * pDelta.X;
                        var squareY = pDelta.Y * pDelta.Y;

                        var radiiScale = squareX / squareRadiusWidth + squareY / squareRadiusHeight;

                        if (radiiScale > 1)
                        {
                            radiiScale = Math.Sqrt(radiiScale);
                            at.Radius = new Size(
                                at.Radius.Width * radiiScale,
                                at.Radius.Height * radiiScale);
                        }
                    }

                    Point e1, e2;
                    at.GetEllipseCenters(pp, out e1, out e2);

                    var ellipseCenter = (at.LargeArc ^ at.SweepClockwise) ? e1 : e2;
                    var rect = new Rect(ellipseCenter - at.Radius, at.Radius * 2);

                    if (debug)
                    {
                        //var ellipseCenter2 = (at.LargeArc ^ at.SweepClockwise) ? e2 : e1;
                        //var rect2 = new Rect(ellipseCenter2 - at.Radius, at.Radius * 2);

                        path.AddCircle((float)pp.X, (float)pp.Y, 5, global::Android.Graphics.Path.Direction.Cw);
                        path.AddCircle((float)p.X, (float)p.Y, 5, global::Android.Graphics.Path.Direction.Cw);

                        path.AddCircle((float)ellipseCenter.X, (float)ellipseCenter.Y, 3, global::Android.Graphics.Path.Direction.Cw);
                        path.AddCircle((float)ellipseCenter.X, (float)ellipseCenter.Y, (float)Math.Max(at.Radius.Width, at.Radius.Height), global::Android.Graphics.Path.Direction.Cw);
                        path.AddOval(Conversions.GetRectF(rect), global::Android.Graphics.Path.Direction.Cw);
                        path.AddRect(Conversions.GetRectF(rect), global::Android.Graphics.Path.Direction.Cw);

                        //path.AddCircle((float)ellipseCenter2.X, (float)ellipseCenter2.Y, 3, global::Android.Graphics.Path.Direction.Cw);
                        //path.AddCircle((float)ellipseCenter2.X, (float)ellipseCenter2.Y, (float)Math.Max(at.Radius.Width, at.Radius.Height), global::Android.Graphics.Path.Direction.Cw);
                        //path.AddOval(Conversions.GetRectF(rect2), global::Android.Graphics.Path.Direction.Cw);
                        //path.AddRect(Conversions.GetRectF(rect2), global::Android.Graphics.Path.Direction.Cw);
                    }

                    // Android undocumented behavior fix
                    // project point from ellipse onto circle with same center and radius max(ellipseRadius.Width, ellipseRadius.Height). Projection is away from center of circle.
                    {
                        // x^2 + y^2 = r^2
                        // y^2 = r^2 - x^2
                        // y = root (r^2 - x^2)

                        var circleRadius = Math.Max(at.Radius.Width, at.Radius.Height);

                        if (at.Radius.Width > at.Radius.Height)
                        {
                            var ppXOff = pp.X - ellipseCenter.X;
                            var pXOff = p.X - ellipseCenter.X;
                            var ppY = (float)Math.Sqrt(Math.Max(0, circleRadius * circleRadius - ppXOff * ppXOff));
                            var pY = (float)Math.Sqrt(Math.Max(0, circleRadius * circleRadius - pXOff * pXOff));

                            pp = new Point(
                                pp.X,
                                pp.Y > ellipseCenter.Y ? ellipseCenter.Y + ppY : ellipseCenter.Y - ppY);
                            p = new Point(
                                p.X,
                                p.Y > ellipseCenter.Y ? ellipseCenter.Y + pY : ellipseCenter.Y - pY);
                        }
                        else if (at.Radius.Width < at.Radius.Height)
                        {
                            var ppYOff = pp.Y - ellipseCenter.Y;
                            var pYOff = p.Y - ellipseCenter.Y;
                            var ppX = (float)Math.Sqrt(circleRadius * circleRadius - ppYOff * ppYOff);
                            var pX = (float)Math.Sqrt(circleRadius * circleRadius - pYOff * pYOff);

                            pp = new Point(
                                pp.X > ellipseCenter.X ? ellipseCenter.X + ppX : ellipseCenter.X - ppX,
                                pp.Y);
                            p = new Point(
                                p.X > ellipseCenter.X ? ellipseCenter.X + pX : ellipseCenter.X - pX,
                                p.Y);
                        }
                    }

                    var startVec = (pp - ellipseCenter).Normalized;
                    var endVec = (p - ellipseCenter).Normalized;

                    // see https://au.mathworks.com/matlabcentral/answers/180131-how-can-i-find-the-angle-between-two-vectors-including-directional-information#answer_169149
                    var startAngle = Conversions.RadToDeg(-1 * (float)Math.Atan2(-startVec.Y, startVec.X));
                    var endAngle = Conversions.RadToDeg(-1 * (float)Math.Atan2(-endVec.Y, endVec.X));

                    if (debug)
                    {
                        path.AddCircle((float)pp.X, (float)pp.Y, 5, global::Android.Graphics.Path.Direction.Cw);
                        path.AddCircle((float)p.X, (float)p.Y, 5, global::Android.Graphics.Path.Direction.Cw);

                        path.MoveTo((float)ellipseCenter.X, (float)ellipseCenter.Y);
                        var end = startVec * 400;
                        path.RLineTo((float)end.X, (float)end.Y);
                        path.MoveTo((float)ellipseCenter.X, (float)ellipseCenter.Y);
                        end = endVec * 400;
                        path.RLineTo((float)end.X, (float)end.Y);
                    }

                    var sweepAngle = endAngle - startAngle;

                    if (at.SweepClockwise && sweepAngle < 0)
                    {
                        // If we want to go CW, sweepAngle needs to be positive
                        sweepAngle += 360.0f;
                    }
                    else if (!at.SweepClockwise && sweepAngle > 0)
                    {
                        // If we want to go CCW, sweepAngle needs to be negative
                        sweepAngle -= 360.0f;
                    }

                    path.AddArc(Conversions.GetRectF(rect), (float)startAngle, (float)sweepAngle);

                    bb.Add(p);
                    prevPoint = p;
                    continue;
                }
                var ct = op as CurveTo;
                if (ct != null)
                {
                    var p = ct.Point;
                    var c1 = ct.Control1;
                    var c2 = ct.Control2;
                    path.CubicTo((float)c1.X, (float)c1.Y, (float)c2.X, (float)c2.Y, (float)p.X, (float)p.Y);
                    bb.Add(p);
                    bb.Add(c1);
                    bb.Add(c2);
                    prevPoint = p;
                    continue;
                }
                var cp = op as ClosePath;
                if (cp != null)
                {
                    path.Close();
                    continue;
                }

                throw new NotSupportedException("Path Op " + op);
            }

            return bb;
        }

        private static readonly Pen debugPen = new Pen(Color.FromRGB(0.1, 0.1, 0.9, 0.5));

        public void DrawPath(IEnumerable<PathOp> ops, Pen pen = null, Brush brush = null)
        {
            if (pen == null && brush == null)
                return;

            global::Android.Graphics.Path path;
            var bb = ComputePath(ops, out path);

            using (path)
            {
                var frame = bb.BoundingBox;

                if (brush != null)
                {
                    var paint = GetBrushPaint(brush, frame);
                    graphics.DrawPath(path, paint);
                }
                if (pen != null)
                {
                    var paint = GetPenPaint(pen);
                    graphics.DrawPath(path, paint);
                }
            }

            //global::Android.Graphics.Path debugPath;
            //bb = ComputePath(ops, out debugPath, true);

            //using (debugPath)
            //{
            //    var frame = bb.BoundingBox;

            //    var paint = GetPenPaint(debugPen);
            //    graphics.DrawPath(debugPath, paint);
            //}
        }

        public void DrawRectangle(Rect frame, Size corner, Pen pen = null, Brush brush = null)
        {
            var left = (float)(frame.X);
            var top = (float)(frame.Y);
            var right = (float)(frame.X + frame.Width);
            var bottom = (float)(frame.Y + frame.Height);
            var rx = (float)corner.Width;
            var ry = (float)corner.Height;
            if (brush != null)
            {
                var paint = GetBrushPaint(brush, frame);
                if (rx > 0 || ry > 0)
                {
                    graphics.DrawRoundRect(new RectF(left, top, right, bottom), rx, ry, paint);
                }
                else
                {
                    graphics.DrawRect(left, top, right, bottom, paint);
                }
            }
            if (pen != null)
            {
                var paint = GetPenPaint(pen);
                if (rx > 0 || ry > 0)
                {
                    graphics.DrawRoundRect(new RectF(left, top, right, bottom), rx, ry, paint);
                }
                else
                {
                    graphics.DrawRect(left, top, right, bottom, paint);
                }
            }
        }
        public void DrawEllipse(Rect frame, Pen pen = null, Brush brush = null)
        {
            if (brush != null)
            {
                var paint = GetBrushPaint(brush, frame);
                graphics.DrawOval(Conversions.GetRectF(frame), paint);
            }
            if (pen != null)
            {
                var paint = GetPenPaint(pen);
                graphics.DrawOval(Conversions.GetRectF(frame), paint);
            }
        }
        public void DrawImage(IImage image, Rect frame, double alpha = 1.0)
        {
            var ii = image as BitmapImage;
            if (ii != null)
            {
                var paint = GetImagePaint(alpha);
                var isize = new Size(ii.Bitmap.Width, ii.Bitmap.Height);
                var scale = frame.Size / isize;
                var m = new Matrix();
                m.PreTranslate((float)frame.X, (float)frame.Y);
                m.PreScale((float)scale.Width, (float)scale.Height);
                graphics.DrawBitmap(ii.Bitmap, m, paint);
            }
        }
    }

    public static class Conversions
    {
        public static PointF GetPointF(this Point point)
        {
            return new PointF((float)point.X, (float)point.Y);
        }

        public static RectF GetRectF(this Rect frame)
        {
            return new RectF((float)frame.X, (float)frame.Y, (float)(frame.X + frame.Width), (float)(frame.Y + frame.Height));
        }

        public static float RadToDeg(float rad)
        {
            return (float)(rad / Math.PI * 180);
        }
    }
}


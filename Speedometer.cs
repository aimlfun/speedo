using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace Speedo;

/// <summary>
/// Implementation of a speedometer, that is comprised of a face (part of a static image) and a needle.
/// Features: Aesthetics, a dampened pointy-needle, lcd display, perf from caching of background.
/// </summary>
internal static class DashboardSpeedometer
{
    /// <summary>
    /// How much the needle sweeps.
    /// </summary>
    private const int c_needleSweepDegrees = 270;

    /// <summary>
    /// What angle the start of the seep is at.
    /// </summary>
    private const int c_needleStartDegrees = 137;

    /// <summary>
    /// Maximum MPH on the dial.
    /// </summary>
    private const int c_maxMPHonDial = 140;

    /// <summary>
    /// Size of the dial. Be careful changing it as some logic may need to change.
    /// </summary>
    private const int c_sizeOfDial = 95;

    /// <summary>
    /// We cache the speedo background to avoid repainting every time the needle moves.
    /// </summary>
    private static Bitmap? s_backgroundSpeedo = null;

    /// <summary>
    /// Used to draw the font on the dial AND the LCD.
    /// </summary>
    private readonly static Font s_dialFont = new("Arial", 11, FontStyle.Bold);

    /// <summary>
    /// Used to draw a shadow under the needle.
    /// </summary>
    private readonly static SolidBrush s_needleShadowBrush = new(Color.FromArgb(190, 0, 0, 0));

    /// <summary>
    /// Used to draw the needle (orangy-red)
    /// </summary>
    private readonly static SolidBrush s_needleBrush = new(Color.FromArgb(240, 188, 44, 54));

    /// <summary>
    /// Center of the dial.
    /// </summary>
    private static Point s_centerOfDial;

    /// <summary>
    /// We track this, so we can smoothly move the needle in direction of travel.
    /// </summary>
    private static double s_lastSpeedDialIndicates = 0;

    /// <summary>
    /// This is the rectangle the LCD occupies.
    /// </summary>
    private static RectangleF s_digitalLCDGauge = new();

    /// <summary>
    /// Used for writing the guage
    /// </summary>
    private readonly static StringFormat s_sfDigitalLCDGauge = new();

    /// <summary>
    /// Sure black on LCD background doesn't look awesomely clear like paper white eInk, but it's the style we're attempting.
    /// </summary>
    private readonly static SolidBrush s_LCDFontColor = new(Color.Black);

    /// <summary>
    /// Draws the speedo face.
    /// </summary>
    /// <param name="g">Graphics to draw the gauge on.</param>
    /// <param name="center">Center point of the dial.</param>
    /// <param name="sizeOfDial">Size of the dial.</param>
    private static Bitmap DrawSpeedoFace(Point center)
    {
        Bitmap bitmapSpeedo = new(c_sizeOfDial * 2 + 50, c_sizeOfDial * 2 + 50);

        using Graphics graphics = Graphics.FromImage(bitmapSpeedo);
        ToHighQuality(graphics);

        // do this once, not per needle
        s_sfDigitalLCDGauge.LineAlignment = StringAlignment.Center;
        s_sfDigitalLCDGauge.Alignment = StringAlignment.Center;

        double cos, sin;

        s_centerOfDial = center;

        int cx = center.X;
        int cy = center.Y;

        DrawGaugeCircleWithMetalEdge(graphics, cx, cy);

        using Pen penMajorGraduations = new(Color.Black, 3); // thicker lines for 10, 20, 30, 40
        using Pen penMinorGraduations = new(Color.Black, 1); // thin lines 1, 2, 3.. 9. We make 5 slightly longer.

        float sweepAngle10mphSweepsOnDial = c_needleSweepDegrees / (c_maxMPHonDial / 10);

        int mphToDrawOnDial = 0;

        // drawn around a dial
        //
        // major graduations (every 10 mph)
        // |....:....|....:....|....:....|...
        // ^         ^         ^         ^
        // 0         10        20        30
        // 0         --        20        --  we write "0" and every "20" (aesthetics)
        for (float angle10mph = 0; angle10mph <= c_needleSweepDegrees; angle10mph += sweepAngle10mphSweepsOnDial)
        {
            float angle1mphRequires = sweepAngle10mphSweepsOnDial / 10;

            int count = 0;

            float angleLine = angle10mph + c_needleStartDegrees;

            // |....:....|....:....|....:....|...
            //  [-------] [-------] [-------] [--  each are a "sweep"
            for (float angle1mph = 0; angle1mph < sweepAngle10mphSweepsOnDial; angle1mph += angle1mphRequires)
            {
                if (angleLine + angle1mph >= c_needleSweepDegrees + 133) continue;

                double angleInRadians = MathUtils.DegreesInRadians(angleLine + angle1mph);

                // we use this twice, as we're drawing a line from the outer-edge inwards (think of it as part of the radius)
                cos = Math.Cos(angleInRadians);
                sin = Math.Sin(angleInRadians);

                //      +         +         +   draw lines at these points   
                // |....:....|....:....|....:....|...
                //      ^ count==5^      ^ count != 5
                int r = 75 - (count == 5 ? 5 : 0); // 5 = halfway between 10 markings, we make this LONGER 

                graphics.DrawLine(penMinorGraduations, (float)(cx + 83 * cos), (float)(cy + 83 * sin), (float)(cx + r * cos), (float)(cy + r * sin));

                ++count; // so we know which marking it is.
            }

            cos = Math.Cos(MathUtils.DegreesInRadians(angleLine));
            sin = Math.Sin(MathUtils.DegreesInRadians(angleLine));

            // |....:....|....:....|....:....|...
            // ^         ^         ^         ^ draw these
            graphics.DrawLine(penMajorGraduations, (float)(cx + 83 * cos), (float)(cy + 83 * sin), (float)(cx + 65 * cos), (float)(cy + 65 * sin));

            // we've chosen to draw every 20 mph (i.e. 0, 20, 40, 60 .. 140). It looks more beautiful than cramming in every 10.
            if (mphToDrawOnDial % 20 == 0)
            {
                string speedAsString = mphToDrawOnDial.ToString();
                SizeF size = graphics.MeasureString(speedAsString, s_dialFont); // we need to adjust positioning based on text size

                // positioning digits next to graduations requires the same radius rotation logic as drawing the line, just with a smaller radius.
                graphics.DrawString(speedAsString, s_dialFont, Brushes.Black, (float)(cx + 53 * cos) - size.Width / 2, (float)(cy + 53 * sin) - size.Height / 2);
            }

            mphToDrawOnDial += 10; // each graduation is worth 10mph
        }

        // center dial
        graphics.FillEllipse(new SolidBrush(Color.FromArgb(160, 0, 0, 0)), cx - 15 + 2, cy - 15 + 2, 30, 30); // subtle shadow from centre
        graphics.FillEllipse(new SolidBrush(Color.Black), cx - 15, cy - 15, 30, 30);

        DrawLCDpartofGauge(graphics, cx, cy);

        return bitmapSpeedo;
    }

    /// <summary>
    /// This creates the "LCD" part of the guage at the bottom of the dial.
    /// </summary>
    /// <param name="graphics"></param>
    /// <param name="cx"></param>
    /// <param name="cy"></param>
    private static void DrawLCDpartofGauge(Graphics graphics, int cx, int cy)
    {
        // calculate the LCD gauge 
        s_digitalLCDGauge = new RectangleF(cx - 30, cy + c_sizeOfDial * 0.4F + 10, 60, 20);

        // [  89  ], colour acquired from photo of LCD
        graphics.FillRectangle(new SolidBrush(Color.FromArgb(114, 98, 85)), s_digitalLCDGauge);

        // shadow top and left of LCD (consistent with other shadows)
        graphics.DrawLine(new Pen(Color.FromArgb(100, 0, 0, 0), 2),
                   s_digitalLCDGauge.X, s_digitalLCDGauge.Y + 1,
                   s_digitalLCDGauge.X + s_digitalLCDGauge.Width, s_digitalLCDGauge.Y + 1);

        graphics.DrawLine(new Pen(Color.FromArgb(100, 0, 0, 0), 2),
                   s_digitalLCDGauge.X + 1, s_digitalLCDGauge.Y,
                   s_digitalLCDGauge.X + 1, s_digitalLCDGauge.Y + s_digitalLCDGauge.Height);
    }

    /// <summary>
    /// This provides the face of the gauge, with a nice edging.
    /// </summary>
    /// <param name="graphics"></param>
    /// <param name="cx"></param>
    /// <param name="cy"></param>
    private static void DrawGaugeCircleWithMetalEdge(Graphics graphics, int cx, int cy)
    {
        // black around the gauge
        graphics.DrawEllipse(new Pen(Color.FromArgb(160, 200, 200, 200), 1), cx - 101, cy - 101, 101 * 2, 101 * 2);
        graphics.DrawEllipse(new Pen(Color.FromArgb(190, 0, 0, 0), 5), cx - 99, cy - 99, 99 * 2, 99 * 2);
        graphics.DrawEllipse(new Pen(Color.FromArgb(230, 0, 0, 0), 4), cx - 98, cy - 98, 98 * 2, 98 * 2);
        graphics.DrawEllipse(new Pen(Color.Black, 2), cx - 96, cy - 96, 96 * 2, 96 * 2);

        // draw the beautiful background colour for the gauge.
        graphics.FillEllipse(new SolidBrush(Color.FromArgb(240, 219, 188)), cx - 90, cy - 90, 180, 180);

        // add a subtle shadow
        graphics.DrawEllipse(new Pen(Color.FromArgb(50, 0, 0, 0), 5), cx - 90, cy - 90, 180, 180);

        // draw the 3d looking metal around the gauge. This was the result of trial and error.
        graphics.DrawEllipse(new Pen(Color.FromArgb(255, 150, 150, 150), 9), cx - 95, cy - 95, 190, 190);
        graphics.DrawEllipse(new Pen(Color.FromArgb(255, 200, 200, 200), 7), cx - 95, cy - 95, 190, 190);
        graphics.DrawEllipse(new Pen(Color.FromArgb(255, 220, 220, 220), 5), cx - 97, cy - 95, 190, 190);
        graphics.DrawEllipse(new Pen(Color.FromArgb(255, 240, 240, 240), 3), cx - 96, cy - 95, 190, 190);

        graphics.DrawEllipse(new Pen(Color.FromArgb(10, 0, 0, 0), 2), cx - 95 + 1, cy - 95 + 1, 190, 190);

        graphics.DrawEllipse(new Pen(Color.FromArgb(100, 0, 0, 0), 1), cx - 95, cy - 95, 190, 190);
    }

    /// <summary>
    /// Draws the speedo needle and speedo if necessary.
    /// </summary>
    /// <param name="center">Centre of the dial.</param>
    /// <param name="speed">Speed to represent using the needle and LCD display.</param>
    internal static Bitmap DrawNeedle(Point center, double speed)
    {
        // if we haven't painted the background, we need to do that first.
        s_backgroundSpeedo ??= DrawSpeedoFace(center);

        Bitmap bitmapSpeedo = new(s_backgroundSpeedo); // don't wrap with "using", we are returning this

        //return bitmapSpeedo;

        using Graphics graphics = Graphics.FromImage(bitmapSpeedo);
        ToHighQuality(graphics);

        const int maxNeedleDeflectionAllowed = 1;

        // damp the needle, by only allowing it to move by a limited amount. It will always eventually reach the speed unless the speed reduces.
        s_lastSpeedDialIndicates = MathUtils.Clamp(speed, s_lastSpeedDialIndicates - maxNeedleDeflectionAllowed, s_lastSpeedDialIndicates + maxNeedleDeflectionAllowed);

        // the range is 0..140 spread over 270 degrees, starting at (c_needleStartDegrees approx 137 degrees)
        double angle = c_needleStartDegrees + (s_lastSpeedDialIndicates / 140 * (c_needleSweepDegrees - 5));

        if (angle < 0) angle += 360;
        if (angle > 360) angle -= 360;

        // alas my brain works in degrees, and sin/cos require radians. (yes 2*PI=360, but whatever)
        double angleRadians = MathUtils.DegreesInRadians(angle);

        // we're painting a needle. It isn't quite a triangle (more a stretched trapezium), the point has a
        // thickness of +/- 0.5 which is thick enough, so we require 2 points 
        double dialTriangleAngleTip = 0.5 * 0.0349066F;  // radians :)

        // size of dial - 15px means it points into the small graduations            
        float tipOfNeedleX1 = (float)Math.Cos(angleRadians - dialTriangleAngleTip) * (c_sizeOfDial - 15) + s_centerOfDial.X;
        float tipOfNeedleY1 = (float)Math.Sin(angleRadians - dialTriangleAngleTip) * (c_sizeOfDial - 15) + s_centerOfDial.Y;

        float tipOfNeedleX2 = (float)Math.Cos(angleRadians + dialTriangleAngleTip) * (c_sizeOfDial - 15) + s_centerOfDial.X;
        float tipOfNeedleY2 = (float)Math.Sin(angleRadians + dialTriangleAngleTip) * (c_sizeOfDial - 15) + s_centerOfDial.Y;

        int dialCenterRadius = 15;

        // this defines the angle split between the edges of the needle
        double dialTriangleAngle = 7 * 0.0349066F; // radians

        // two points where it connects to the center dial.
        float centrePartOfNeedleX1 = s_centerOfDial.X + (float)Math.Cos(angleRadians - dialTriangleAngle) * dialCenterRadius;
        float centrePartOfNeedleY1 = s_centerOfDial.Y + (float)Math.Sin(angleRadians - dialTriangleAngle) * dialCenterRadius;

        float centrePartOfNeedleX2 = s_centerOfDial.X + (float)Math.Cos(angleRadians + dialTriangleAngle) * dialCenterRadius;
        float centrePartOfNeedleY2 = s_centerOfDial.Y + (float)Math.Sin(angleRadians + dialTriangleAngle) * dialCenterRadius;

        PointF[] pointsForNeedle = { new PointF(centrePartOfNeedleX1, centrePartOfNeedleY1),
                                     new PointF(tipOfNeedleX1, tipOfNeedleY1),
                                     new PointF(tipOfNeedleX2, tipOfNeedleY2),
                                     new PointF(centrePartOfNeedleX2, centrePartOfNeedleY2) };

        // same as needle but a very simple +2 offset shadow. Technically it's wrong, as the amount of shadow depends on where the light source is in
        // respect to the needle, but that's too much effort for now to fix.
        PointF[] pointsForShadowUnderNeedle = { new PointF(centrePartOfNeedleX1 + 2, centrePartOfNeedleY1 + 2),
                                                new PointF(tipOfNeedleX1 + 2, tipOfNeedleY1 + 2),
                                                new PointF(tipOfNeedleX2 + 2, tipOfNeedleY2 + 2),
                                                new PointF(centrePartOfNeedleX2 + 2, centrePartOfNeedleY2 + 2) };

        // triangle-ish needle (point is flattened, so trapezium)
        graphics.FillPolygon(s_needleShadowBrush, pointsForShadowUnderNeedle); // shadow beneath the needle
        graphics.FillPolygon(s_needleBrush, pointsForNeedle); // the needle

        // write the speed to the LCD 
        graphics.DrawString(Math.Round(speed).ToString(), s_dialFont, s_LCDFontColor,
                     new RectangleF(s_digitalLCDGauge.X, s_digitalLCDGauge.Y + 2, //+2, as it doesn't quite look centred vertically
                                    s_digitalLCDGauge.Width, s_digitalLCDGauge.Height - 2), 
                                    s_sfDigitalLCDGauge); 
        graphics.Flush();

        return bitmapSpeedo;
    }

    /// <summary>
    /// Set graphics setting to add anti-aliasing etc, to void pixellation (chunky lines / circles).
    /// </summary>
    /// <param name="graphics"></param>
    private static void ToHighQuality(Graphics graphics)
    {
        if (graphics.InterpolationMode == InterpolationMode.HighQualityBicubic) return; // saves 5 assigns each call

        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.CompositingQuality = CompositingQuality.HighQuality;
        graphics.SmoothingMode = SmoothingMode.HighQuality;
        graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
    }
}
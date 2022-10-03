namespace Speedo;

public partial class Form1 : Form
{
    int speed = 0;
    int dir = 1;

    public Form1()
    {
        InitializeComponent();

        timer1.Enabled = true;
        timer1.Interval = 20;
        timer1.Tick += Timer1_Tick;
    }

    private void Timer1_Tick(object? sender, EventArgs e)
    {
        speed += dir;
        if (speed < 1 || speed > 139) dir = -dir;
        speed = 100;
        Bitmap b = DashboardSpeedometer.DrawNeedle(new Point(pictureBox1.Width / 2, pictureBox1.Height / 2), speed);

        pictureBox1.Image?.Dispose();
        pictureBox1.Image = b;
    }
}
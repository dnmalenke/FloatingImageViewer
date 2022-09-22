using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Resources;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace FloatingImageViewer
{

    internal sealed class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            if (args.Length > 0)
            {
                Application.Run(new ImageForm(args[0]));
            }
        }
    }

    internal sealed class ImageForm : Form
    {
        private Panel _panel = new()
        {
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
            Location = new Point(2, 2),
            BackColor = Color.Transparent,
        };

        private PictureBox _pictureBox = new()
        {
            SizeMode = PictureBoxSizeMode.Zoom,
            Dock = DockStyle.Fill,
        };

        private bool _mouseDown = false;
        private Point _lastPoint = Point.Empty;

        public ImageForm(string imagePath)
        {
            FormBorderStyle = FormBorderStyle.None;

            _pictureBox.LoadCompleted += _pictureBox_LoadCompleted;
            _pictureBox.MouseDown += _pictureBox_MouseDown;
            _pictureBox.MouseMove += _pictureBox_MouseMove;
            _pictureBox.MouseUp += _pictureBox_MouseUp;
            _pictureBox.MouseWheel += _pictureBox_MouseWheel;
            _pictureBox.ImageLocation = imagePath;

            _pictureBox.ContextMenuStrip = new ContextMenuStrip();
            _pictureBox.ContextMenuStrip.Items.Add("Close").Click += (s, e) =>
            {
                Close();
            };

            _pictureBox.ContextMenuStrip.Items.Add("Maximize").Click += (s, e) =>
            {
                WindowState = WindowState == FormWindowState.Maximized ? WindowState = FormWindowState.Normal : FormWindowState.Maximized;
                ToolStripMenuItem? menuItem = (ToolStripMenuItem?)s;
                if (menuItem != null)
                {
                    menuItem.Text = WindowState == FormWindowState.Maximized ? "Un-Maximize" : "Maximize";
                }
            };

            _pictureBox.ContextMenuStrip.Items.Add("Draw").Click += (s, e) =>
            {
                Cursor = Cursor == Cursors.Cross ? Cursors.Default : Cursors.Cross;
                ToolStripMenuItem? menuItem = (ToolStripMenuItem?)s;
                if (menuItem != null)
                {
                    menuItem.Text = Cursor == Cursors.Cross ? "Stop Drawing" : "Draw";
                }
            };

            _pictureBox.ContextMenuStrip.Items.Add("Clear Drawing").Click += (s, e) =>
            {
                _pictureBox.Load();
            };

            _panel.Controls.Add(_pictureBox);
            Controls.Add(_panel);

            BackColor = Color.Black;

            TopMost = true;
        }

        private void _pictureBox_MouseUp(object? sender, MouseEventArgs e)
        {
            _mouseDown = false;
            _lastPoint = Point.Empty;
        }

        private Pen _drawPen = new Pen(Color.Red, 2);

        private void _pictureBox_MouseMove(object? sender, MouseEventArgs e)
        {
            if (_mouseDown && Cursor == Cursors.Cross && _lastPoint != Point.Empty)
            {
                using Graphics g = Graphics.FromImage(_pictureBox.Image);
                g.DrawLine(_drawPen, ScalePoint(_lastPoint), ScalePoint(e.Location));
                g.SmoothingMode = SmoothingMode.AntiAlias;

                _pictureBox.Invalidate();//refreshes the picturebox

                _lastPoint = e.Location;//keep
            }
        }

        private Point ScalePoint(Point p)
        {
            return new Point(p.X * (_pictureBox.Image.Size.Width + BORDER_SIZE) / Width, p.Y * (_pictureBox.Image.Size.Height + BORDER_SIZE) / Height);
        }

        private void _pictureBox_MouseWheel(object? sender, MouseEventArgs e)
        {

        }

        public const int BORDER_SIZE = 3;
        public const int WM_NCLBUTTONDOWN = 0xA1;
        public const int HT_CAPTION = 0x2;

        [DllImport("user32.dll")]
        public static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
        [DllImport("user32.dll")]
        public static extern bool ReleaseCapture();

        private void _pictureBox_MouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                if (Cursor != Cursors.Cross)
                {
                    ReleaseCapture();
                    SendMessage(Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
                }
                else
                {
                    _mouseDown = true;
                    _lastPoint = e.Location;
                }
            }
        }

        private void _pictureBox_LoadCompleted(object? sender, System.ComponentModel.AsyncCompletedEventArgs e)
        {
            Size = new Size(_pictureBox.Image.Size.Width + BORDER_SIZE, _pictureBox.Image.Size.Height + BORDER_SIZE);
            Controls[0].Size = new Size(Width - BORDER_SIZE, Height - BORDER_SIZE);
        }

        protected override void WndProc(ref Message m)
        {
            switch (m.Msg)
            {
                case 0x0084/*NCHITTEST*/ :
                    base.WndProc(ref m);

                    if ((int)m.Result == 0x01/*HTCLIENT*/)
                    {
                        Point screenPoint = new Point(m.LParam.ToInt32());
                        Point clientPoint = PointToClient(screenPoint);
                        if (clientPoint.Y <= BORDER_SIZE)
                        {
                            if (clientPoint.X <= BORDER_SIZE)
                                m.Result = (IntPtr)13/*HTTOPLEFT*/ ;
                            else if (clientPoint.X < (Size.Width - BORDER_SIZE))
                                m.Result = (IntPtr)12/*HTTOP*/ ;
                            else
                                m.Result = (IntPtr)14/*HTTOPRIGHT*/ ;
                        }
                        else if (clientPoint.Y <= (Size.Height - BORDER_SIZE))
                        {
                            if (clientPoint.X <= BORDER_SIZE)
                                m.Result = (IntPtr)10/*HTLEFT*/ ;
                            else if (clientPoint.X < (Size.Width - BORDER_SIZE))
                                m.Result = (IntPtr)2/*HTCAPTION*/ ;
                            else
                                m.Result = (IntPtr)11/*HTRIGHT*/ ;
                        }
                        else
                        {
                            if (clientPoint.X <= BORDER_SIZE)
                                m.Result = (IntPtr)16/*HTBOTTOMLEFT*/ ;
                            else if (clientPoint.X < (Size.Width - BORDER_SIZE))
                                m.Result = (IntPtr)15/*HTBOTTOM*/ ;
                            else
                                m.Result = (IntPtr)17/*HTBOTTOMRIGHT*/ ;
                        }
                    }
                    return;
                // WM_SIZING
                case 0x214:
                    RECT? nullRC = (RECT?)Marshal.PtrToStructure(m.LParam, typeof(RECT));

                    if (nullRC != null)
                    {
                        RECT rc = nullRC.Value;
                        if ((rc.Right - rc.Left) != Width) // x growing
                        {
                            rc.Bottom = rc.Top + (int)((rc.Right - rc.Left) / ((double)(_pictureBox.Image.Size.Width + BORDER_SIZE) / (_pictureBox.Image.Size.Height + BORDER_SIZE)));
                        }
                        else if ((rc.Bottom - rc.Top) != Height)
                        {
                            rc.Right = rc.Left + (int)((rc.Bottom - rc.Top) / ((double)(_pictureBox.Image.Size.Height + BORDER_SIZE) / (_pictureBox.Image.Size.Width + BORDER_SIZE)));
                        }

                        Marshal.StructureToPtr(rc, m.LParam, false);
                    }
                    m.Result = (IntPtr)1;
                    return;
            }
            base.WndProc(ref m);
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.Style |= 0x20000;
                cp.ClassStyle |= 0x20000; // drop shadow
                return cp;
            }
        }
    }
}
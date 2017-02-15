﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using IPlugins;
using Brush = System.Drawing.Brush;
using Brushes = System.Windows.Media.Brushes;
using Button = System.Windows.Controls.Button;
using Color = System.Drawing.Color;
using FontFamily = System.Drawing.FontFamily;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Pen = System.Drawing.Pen;
using PixelFormat = System.Drawing.Imaging.PixelFormat;
using Point = System.Drawing.Point;
using Size = System.Drawing.Size;

namespace Drawing
{
    /// <summary>
    /// BinaryWindow.xaml 的交互逻辑
    /// </summary>
    public partial class DrawingWindow
    {
        #region Private fields

        private bool _resize;
        private int _oldWidth;
        private int _oldHeight;

        private readonly DrawingManager _drawingManager;
        private Button _selectedButton;
        private DrawingTool _drawingTool;
        private bool _mouseCaptured;
        private bool _drawingChanged;
        private Point _drawStartPoint;
        private Point _drawCurrentPoint;
        private readonly List<Point> _linePoints;
        private Color _pickedColor;

        #endregion

        #region Constructor

        public DrawingWindow(Bitmap bitmap)
        {
            InitializeComponent();
            var resizeBitmap = ResizeBitmap(bitmap);
            var screenHeight = SystemParameters.VirtualScreenHeight;
            var screenWidth = SystemParameters.VirtualScreenWidth;
            
            var height = resizeBitmap.Height + 210.0;
            var width = resizeBitmap.Width + 40.0;
            if (height < 370)
            {
                height = 370;
            }
            else if (height > screenHeight)
            {
                height = screenHeight;
            }
            if (width < 370)
            {
                width = 370;
            }
            else if (width > screenWidth)
            {
                width = screenWidth;
            }
            Height = height;
            Width = width;
            ImageVisulGrid.Height = resizeBitmap.Height;
            ImageVisulGrid.Width = resizeBitmap.Width;

            _linePoints = new List<Point>();
            _drawingChanged = false;
            _selectedButton = PenToolBtn;
            _drawingTool = DrawingTool.PenTool;
            _pickedColor = Color.Transparent;
            _drawingManager = new DrawingManager(TargetImage, resizeBitmap)
            {
                RedoButtuon =  RedoButton,
                UndoButton =  UndoButton,
            };
        }

        #endregion

        #region Protect methods

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var hwndSource = PresentationSource.FromVisual(this) as HwndSource;
            hwndSource?.AddHook(WndProc);
        }

        protected const int WmNchittest = 0x0084;
        protected const int AgWidth = 12;
        protected const int BThickness = 4;

        protected virtual IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            switch (msg)
            {
                case WmNchittest:
                    var mousePoint = new Point(lParam.ToInt32() & 0xFFFF, lParam.ToInt32() >> 16);
                    if (mousePoint.Y - Top <= AgWidth
                       && mousePoint.X - Left <= AgWidth)
                    {
                        handled = true;
                        return new IntPtr((int)HitTest.Httopleft);
                    }
                    else if (ActualHeight + Top - mousePoint.Y <= AgWidth
                       && mousePoint.X - Left <= AgWidth)
                    {
                        handled = true;
                        return new IntPtr((int)HitTest.Htbottomleft);
                    }
                    else if (mousePoint.Y - Top <= AgWidth
                       && ActualWidth + Left - mousePoint.X <= AgWidth)
                    {
                        handled = true;
                        return new IntPtr((int)HitTest.Httopright);
                    }
                    else if (ActualWidth + Left - mousePoint.X <= AgWidth
                       && ActualHeight + Top - mousePoint.Y <= AgWidth)
                    {
                        handled = true;
                        return new IntPtr((int)HitTest.Htbottomright);
                    }
                    else if (mousePoint.X - Left <= BThickness)
                    {
                        handled = true;
                        return new IntPtr((int)HitTest.Htleft);
                    }
                    else if (ActualWidth + Left - mousePoint.X <= BThickness)
                    {
                        handled = true;
                        return new IntPtr((int)HitTest.Htright);
                    }
                    else if (mousePoint.Y - Top <= BThickness)
                    {
                        handled = true;
                        return new IntPtr((int)HitTest.Httop);
                    }
                    else if (ActualHeight + Top - mousePoint.Y <= BThickness)
                    {
                        handled = true;
                        return new IntPtr((int)HitTest.Htbottom);
                    }
                    else
                    {
                        handled = false;
                        return IntPtr.Zero;
                    }
            }
            return IntPtr.Zero;
        }

        protected enum HitTest
        {
            Hterror = -2,
            Httransparent = -1,
            Htnowhere = 0,
            Htclient = 1,
            Htcaption = 2,
            Htsysmenu = 3,
            Htgrowbox = 4,
            Htsize = Htgrowbox,
            Htmenu = 5,
            Hthscroll = 6,
            Htvscroll = 7,
            Htminbutton = 8,
            Htmaxbutton = 9,
            Htleft = 10,
            Htright = 11,
            Httop = 12,
            Httopleft = 13,
            Httopright = 14,
            Htbottom = 15,
            Htbottomleft = 16,
            Htbottomright = 17,
            Htborder = 18,
            Htreduce = Htminbutton,
            Htzoom = Htmaxbutton,
            Htsizefirst = Htleft,
            Htsizelast = Htbottomright,
            Htobject = 19,
            Htclose = 20,
            Hthelp = 21,
        }

        #endregion

        #region Public properties

        public HandleResult HandleResult { get; private set; }

        #endregion

        #region Event

        private void WindowLoaded(object sender, RoutedEventArgs e)
        {
            this.RemoveSystemMenuItems(Win32.SystemMenuItems.Restore | Win32.SystemMenuItems.Minimize | Win32.SystemMenuItems.Maximize | Win32.SystemMenuItems.SpliteLine); //去除窗口指定的系统菜单
            DrawingInfo_ColorChanged(null, null);
            DrawingInfo_FontChanged(null, null);
        }

        private void WindowKeyDown(object sender, KeyEventArgs e)
        {
            if ((e.KeyboardDevice.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                switch (e.Key)
                {
                    case Key.Z:
                        _drawingManager.Undo();
                        break;
                    case Key.C:
                        _drawingManager.LastRecordedBitmap.CopyImageToClipboard();
                        break;
                    case Key.Y:
                        _drawingManager.Redo();
                        break;
                }
            }
        }

        private void DragMoveWindow(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void LeftBtn_Click(object sender, RoutedEventArgs e)
        {
            HandleResult = new HandleResult(null, false);
            Close();
        }

        private void RightBtn_Click(object sender, RoutedEventArgs e)
        {
            HandleResult = new HandleResult(_resize ? _drawingManager.LastRecordedBitmap.ResizeBitmap(_oldWidth, _oldHeight) : (Bitmap)_drawingManager.LastRecordedBitmap.Clone(), true);
            Close();
        }

        private void SettingBtn_Click(object sender, RoutedEventArgs e)
        {
            //...
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e)
        {
            HandleResult = new HandleResult(null, false);
            Close();
        }

        private void DrawingInfo_ColorChanged(object sender, EventArgs e)
        {
            ImageTextBox.Foreground = new SolidColorBrush(DrawingInfo.CurrentArgbColor);
        }

        private void DrawingInfo_FontChanged(object sender, EventArgs e)
        {
            var font = DrawingInfo.CurrentFont;
            ImageTextBox.FontFamily = new System.Windows.Media.FontFamily(font.FontFamily.Name);
            ImageTextBox.FontSize = font.Size;
            if (font.Bold)
            {
                ImageTextBox.FontWeight = FontWeights.Bold;
            }
            if (font.Italic)
            {
                ImageTextBox.FontStyle = FontStyles.Italic;
            }
            var textDecorations = new TextDecorationCollection();
            if (font.Strikeout)
            {
                textDecorations.Add(TextDecorations.Strikethrough);
            }
            if (font.Underline)
            {
                textDecorations.Add(TextDecorations.Underline);
            }
            ImageTextBox.TextDecorations = textDecorations;

        }

        private void DrawingInfo_SelectedRadioChanged(object sender, EventArgs e)
        {
            if(_drawingTool != DrawingTool.PenTool)return;
            if (DrawingInfo.SelectedLeftRadioBtn)
            {
                DrawingInfo.RightSliderLblContent = "画笔大小:";
                DrawingInfo.SetLeftSliderRange(1.0, 20.0);
            }
            else
            {
                DrawingInfo.RightSliderLblContent = "填充偏离度:";
                DrawingInfo.SetLeftSliderRange(0.0, 255.0);
            }
        }

        private void ExchangeBgCbx_Click(object sender, RoutedEventArgs e)
        {
            if (ExchangeBgCbx.IsChecked == true)
            {
                ImageViewGrid.Background = Brushes.White;
                ImageBorder.BorderThickness = new Thickness(0.1);
            }
            else
            {
                ImageViewGrid.Background = Brushes.Transparent;
                ImageBorder.BorderThickness = new Thickness(0);
            }
        }

        private void ExchangeBgCbx_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            var dialog = new ColorDialog();
            if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
            var color = dialog.Color;
            ImageViewGrid.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(DrawingInfo.CurrentArgbColor.A, color.R, color.G, color.B));
        }

        private void ImageButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button == null) return;
            var tag = (DrawingTool)button.Tag;
            switch (tag)
            {
                case DrawingTool.RectTool:
                case DrawingTool.EllipseTool:
                case DrawingTool.ArrowTool:
                case DrawingTool.PenTool:
                case DrawingTool.TextTool:
                case DrawingTool.EraserTool:
                case DrawingTool.PickerTool:
                    SetDrawingTool(button, tag);
                    break;
                case DrawingTool.CopyTool:
                   _drawingManager.LastRecordedBitmap.CopyImageToClipboard();
                    break;
                case DrawingTool.UndoTool:
                    _drawingManager.Undo();
                    break;
                case DrawingTool.RedoTool:
                    _drawingManager.Redo();
                    break;
            }
        }

        private void TargetImage_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var position = e.GetPosition(TargetImage);
            _drawStartPoint.X = (int)Math.Ceiling(position.X);
            _drawStartPoint.Y = (int)Math.Ceiling(position.Y);
            if (_drawStartPoint.X > 0) _drawStartPoint.X--;
            if (_drawStartPoint.Y > 0) _drawStartPoint.Y--;
            if (_drawingTool == DrawingTool.TextTool)
            {
                InsertTextToImage();
                return;
            }
            if (_drawingTool == DrawingTool.PickerTool)
            {
                DrawingInfo.CurrentColor = System.Windows.Media.Color.FromArgb(255, _pickedColor.R, _pickedColor.G, _pickedColor.B);
                DrawingInfo.LeftSliderValue = _pickedColor.A;
                return;
            }
            _mouseCaptured = TargetImage.CaptureMouse();
            if (_drawingTool == DrawingTool.PenTool || _drawingTool == DrawingTool.EraserTool)
            {
                _linePoints.Clear();
                _linePoints.Add(new Point(_drawStartPoint.X, _drawStartPoint.Y));
                TargetImage_MouseMove(sender, e);//画点
            }
            
        }

        private void TargetImage_MouseMove(object sender, MouseEventArgs e)
        {
            var position = e.GetPosition(TargetImage);
            _drawCurrentPoint.X = (int)Math.Ceiling(position.X);
            _drawCurrentPoint.Y = (int)Math.Ceiling(position.Y);
            if (_drawCurrentPoint.X > 0) _drawCurrentPoint.X--;
            if (_drawCurrentPoint.Y > 0) _drawCurrentPoint.Y--;
            TitleLbl.Content = $"Drawing: ({_drawCurrentPoint.X},{ _drawCurrentPoint.Y})";
            if (_mouseCaptured && e.LeftButton == MouseButtonState.Pressed)
            {
                DrawEffects(false);
            }
            else
            {
                if(_drawingTool == DrawingTool.RectTool || _drawingTool == DrawingTool.EllipseTool || _drawingTool == DrawingTool.ArrowTool || _drawingTool == DrawingTool.PenTool || _drawingTool == DrawingTool.EraserTool || _drawingTool == DrawingTool.PickerTool) DrawEffects(true);
            }
           
        }

        private void TargetImage_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_mouseCaptured)
            {
                TargetImage.ReleaseMouseCapture();
                _mouseCaptured = false;
            }
            if (!_drawingChanged) return;
            _drawingChanged = false;
            _drawingManager.Record();
            
        }

        private void TargetImage_MouseLeave(object sender, MouseEventArgs e)
        {
            TitleLbl.Content = "Drawing";
            if (!_mouseCaptured && _drawingTool != DrawingTool.TextTool)//清除预览留下的痕迹
            {
                _drawingManager.ResetCurrentViewBitmap();
                _drawingManager.UpdateView();
            }
        }

        private void ImageTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            ImageTextBox.BorderThickness = new Thickness(0);
            if (ImageTextBox.Text.Trim() == string.Empty)
            {
                ImageTextBox.Visibility = Visibility.Hidden;
                return;
            }
            ImageTextBox.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background,
               new Action(() =>
               {
                   //为了保证描绘图形时ImageTextBox边框为0，所以使用了调度器
                   var renderBitmap = new RenderTargetBitmap(_drawingManager.LastRecordedBitmap.Width, _drawingManager.LastRecordedBitmap.Height, 96, 96, PixelFormats.Pbgra32);
                   renderBitmap.Render(ImageVisulGrid);
                   _drawingManager.Drawing(renderBitmap.GetBitmap());
                   ImageTextBox.Visibility = Visibility.Hidden;
               }));
        }

        #endregion

        #region Private methods

        private Bitmap ResizeBitmap(Bitmap bitmap)
        {
            try
            {
                var width = _oldWidth = bitmap.Width;
                var height =  _oldHeight = bitmap.Height;
                var screenHeight = (int)SystemParameters.VirtualScreenHeight;
                var screenWidth = (int)SystemParameters.VirtualScreenWidth;
                _resize = false;

                if (width < 330 && height < 160)
                {
                    if (width > height)
                    {
                        height = 330 * height / width;
                        width = 330;
                        _resize = true;
                    }
                    else
                    {
                        width = 160 * width / height;
                        height = 160;
                        _resize = true;
                    }
                }

                if (width > screenWidth - 40)
                {
                    height = (screenWidth - 40) * height / width;
                    width = screenWidth - 40;
                    _resize = true;
                }

                if (height > screenHeight - 210)
                {
                    width = (screenHeight - 210) * width / height;
                    height = screenHeight - 210;
                    _resize = true;
                }

                if (!_resize)
                {
                    return bitmap;
                }
                var resizeBitmap = bitmap.ResizeBitmap(width, height);
                bitmap.Dispose();
                return resizeBitmap;
            }
            catch
            {
                return bitmap;
            }
        }

        private void SetDrawingTool(Button selectButton, DrawingTool drawingTool)
        {
            if (_drawingTool == drawingTool) return;
            DrawingInfo.SelectFontVisible(drawingTool == DrawingTool.TextTool);

            Selector.SetIsSelected(_selectedButton, false);
            _selectedButton = selectButton;
            Selector.SetIsSelected(_selectedButton, true);

            _drawingTool = drawingTool;
            if (drawingTool == DrawingTool.ArrowTool)
            {
                DrawingInfo.LeftRadioBtnContent = "箭头";
                DrawingInfo.RightRadioBtnContent = "直线";
            }
            else
            {
                DrawingInfo.LeftRadioBtnContent = "绘制";
                DrawingInfo.RightRadioBtnContent = "填充";
            }
            if (drawingTool == DrawingTool.PenTool && !DrawingInfo.SelectedLeftRadioBtn)
            {
                DrawingInfo.RightSliderLblContent = "填充偏离度:";
                DrawingInfo.SetLeftSliderRange(0.0, 255.0);
            }
            else
            {
                DrawingInfo.RightSliderLblContent = "画笔大小:";
                DrawingInfo.SetLeftSliderRange(1.0, 20.0);
            }
        }

        private void InsertTextToImage()
        {
            if (ImageTextBox.Visibility == Visibility.Hidden)
            {
                ImageTextBox.BorderThickness = new Thickness(1);
                ImageTextBox.Text = string.Empty;
                var width = (int) TargetImage.ActualWidth;
                var height = (int) TargetImage.ActualHeight;
                var left = _drawStartPoint.X;
                var top = _drawStartPoint.Y;
                if (left + 20 > width)
                {
                    left = width - 20;
                    ImageTextBox.MaxWidth = 20;
                }
                else
                {
                    ImageTextBox.MaxWidth = width - left;
                }
                if (top + ImageTextBox.FontSize > height)
                {
                    top = (int) (height - ImageTextBox.FontSize);
                    ImageTextBox.MaxHeight = ImageTextBox.FontSize;
                }
                else
                {
                    ImageTextBox.MaxHeight = height - top;
                }
                ImageTextBox.Margin = new Thickness(left, top, ImageTextBox.Margin.Right, ImageTextBox.Margin.Bottom);
                ImageTextBox.Visibility = Visibility.Visible;
                ImageTextBox.Focus();
            }
            else
            {
                _selectedButton.Focus();
            }
        }

        private void DrawEffects(bool isPreview)
        {
            _drawingManager.ResetCurrentViewBitmap();
            var selectColor = DrawingInfo.CurrentArgbColor;
            if (isPreview)
            {
                if (_drawingTool == DrawingTool.PickerTool)
                {
                    DrawMagnifier();
                }
                else
                {
                    using (var brush = new SolidBrush(Color.FromArgb(selectColor.A, selectColor.R, selectColor.G, selectColor.B)))
                    {
                        using (var pen = new Pen(brush, 1.5f))
                        {
                            using (var g = Graphics.FromImage(_drawingManager.CurrentViewBitmap))
                            {
                                var width = DrawingInfo.RightSliderValue;
                                var rect = new Rectangle(new Point(_drawCurrentPoint.X - (int)(width / 2), _drawCurrentPoint.Y - (int)(width / 2)), new Size((int)width, (int)width));
                                g.DrawRectangle(pen, rect);
                                g.DrawEllipse(pen, rect);

                            }
                        }
                    }
                }
               
                
            }
            else
            {
                var color = Color.FromArgb(selectColor.A, selectColor.R, selectColor.G, selectColor.B);

                Brush brush;
                if (_drawingTool == DrawingTool.EraserTool)
                {
                    brush = System.Drawing.Brushes.Transparent;
                }
                else
                {
                    brush = DrawingInfo.SelectedMosaic ? _drawingManager.MosaicBrush : new SolidBrush(color);
                }
                using (var pen = new Pen(brush, (float)DrawingInfo.RightSliderValue))
                {
                    using (var g = Graphics.FromImage(_drawingManager.CurrentViewBitmap))
                    {
                        if (_drawingTool == DrawingTool.EraserTool)
                        {
                            g.CompositingMode = CompositingMode.SourceCopy;
                        }
                        g.SmoothingMode = SmoothingMode.HighQuality;
                        //g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        g.CompositingQuality = CompositingQuality.HighQuality;
                        Size size;
                        Point leftTopPoint;
                        switch (_drawingTool)
                        {
                            case DrawingTool.RectTool:
                                leftTopPoint = new Point(_drawStartPoint.X < _drawCurrentPoint.X ? _drawStartPoint.X : _drawCurrentPoint.X, _drawStartPoint.Y < _drawCurrentPoint.Y ? _drawStartPoint.Y : _drawCurrentPoint.Y);
                                size = new Size(Math.Abs(_drawCurrentPoint.X - _drawStartPoint.X), Math.Abs(_drawCurrentPoint.Y - _drawStartPoint.Y));
                                if (DrawingInfo.SelectedLeftRadioBtn)
                                {
                                    g.DrawRectangle(pen, new Rectangle(leftTopPoint, size));
                                }
                                else
                                {
                                    g.FillRectangle(brush, new Rectangle(leftTopPoint, size));
                                }
                                break;
                            case DrawingTool.EllipseTool:
                                leftTopPoint = new Point(_drawStartPoint.X < _drawCurrentPoint.X ? _drawStartPoint.X : _drawCurrentPoint.X, _drawStartPoint.Y < _drawCurrentPoint.Y ? _drawStartPoint.Y : _drawCurrentPoint.Y);
                                size = new Size(Math.Abs(_drawCurrentPoint.X - _drawStartPoint.X), Math.Abs(_drawCurrentPoint.Y - _drawStartPoint.Y));
                                if (DrawingInfo.SelectedLeftRadioBtn)
                                {
                                    g.DrawEllipse(pen, new Rectangle(leftTopPoint, size));
                                }
                                else
                                {
                                    g.FillEllipse(brush, new Rectangle(leftTopPoint, size));
                                }
                                break;
                            case DrawingTool.ArrowTool:
                                if (DrawingInfo.SelectedLeftRadioBtn)
                                {
                                    pen.CustomEndCap = pen.Width < 5 ? new AdjustableArrowCap(4f, 5f, true) : new AdjustableArrowCap(1.5f, 2f, true);
                                }
                                g.DrawLine(pen, _drawStartPoint, _drawCurrentPoint);
                                break;
                            case DrawingTool.PenTool:
                                if (DrawingInfo.SelectedLeftRadioBtn)
                                {
                                    if (_linePoints.Count == 1)//画点
                                    {
                                        leftTopPoint = new Point(_drawStartPoint.X - (int)(pen.Width / 2), _drawStartPoint.Y - (int)(pen.Width / 2));
                                        size = new Size((int)pen.Width, (int)pen.Width);
                                        g.FillEllipse(brush, new Rectangle(leftTopPoint, size));
                                        _linePoints.Add(new Point(_drawStartPoint.X, _drawStartPoint.Y));
                                    }
                                    else//画线
                                    {
                                        _linePoints.Add(new Point(_drawCurrentPoint.X, _drawCurrentPoint.Y));
                                        pen.StartCap = pen.EndCap = LineCap.Round;
                                        pen.LineJoin = LineJoin.Round;//很重要,默认的线连接会出现锐角
                                        g.DrawCurve(pen, _linePoints.ToArray());
                                    }
                                }
                                else
                                {
                                    _drawingManager.CurrentViewBitmap.FillFloodColor(_drawStartPoint, color, DrawingInfo.RightSliderValue);
                                }
                                break;
                            case DrawingTool.EraserTool:
                                if (_linePoints.Count == 1)//画点
                                {
                                    leftTopPoint = new Point(_drawStartPoint.X - (int)(pen.Width / 2), _drawStartPoint.Y - (int)(pen.Width / 2));
                                    size = new Size((int)pen.Width, (int)pen.Width);
                                    g.FillEllipse(brush, new Rectangle(leftTopPoint, size));
                                    _linePoints.Add(new Point(_drawStartPoint.X, _drawStartPoint.Y));
                                }
                                else//画线
                                {
                                    _linePoints.Add(new Point(_drawCurrentPoint.X, _drawCurrentPoint.Y));
                                    pen.StartCap = pen.EndCap = LineCap.Round;
                                    pen.LineJoin = LineJoin.Round;//很重要,默认的线连接会出现锐角
                                    g.DrawCurve(pen, _linePoints.ToArray());
                                }
                                break;
                        }
                    }
                }
                _drawingChanged = true;
            }
            _drawingManager.UpdateView();
        }

        private void DrawMagnifier()
        {
            _pickedColor = Color.Transparent;
            var currentViewBitmap = _drawingManager.CurrentViewBitmap;
            if (_drawCurrentPoint.X >= 0 && _drawCurrentPoint.X < currentViewBitmap.Width && _drawCurrentPoint.Y >= 0 && _drawCurrentPoint.Y < currentViewBitmap.Height)
            {
                _pickedColor = currentViewBitmap.GetPixel(_drawCurrentPoint.X, _drawCurrentPoint.Y);
            }
            using (var g = Graphics.FromImage(currentViewBitmap))
            {
                using (var bitmap = new Bitmap(15, 15, PixelFormat.Format32bppArgb))
                {
                    using (var gb = Graphics.FromImage(bitmap))
                    {
                        gb.DrawImage(currentViewBitmap,
                            new Rectangle(Point.Empty, bitmap.Size),
                            new Rectangle(_drawCurrentPoint.X - 7, _drawCurrentPoint.Y - 7, 15, 15),
                            GraphicsUnit.Pixel);
                    }
                    using (var zoomBitmap = bitmap.ZoomBitmap(7))//图像放缩7倍
                    {
                        var height = zoomBitmap.Height;
                        var width = zoomBitmap.Width;
                        var halfWidth = width / 2;
                        var halfHeight = height / 2;
                        const int padding = 3;//内边距
                        const int margin = 10;//外边距 
                        var font = new Font(new FontFamily("宋体"), 9);
                        
                        //放大镜区域
                        var rect = new Rectangle(_drawCurrentPoint.X + margin, _drawCurrentPoint.Y + margin, width + padding * 2, height + padding * 2 + font.Height * 2);
                        if (rect.Right > currentViewBitmap.Width) rect.X = _drawCurrentPoint.X - rect.Width - margin;
                        if (rect.Bottom > currentViewBitmap.Height) rect.Y = _drawCurrentPoint.Y - rect.Height - margin;
                        var brush = new SolidBrush(Color.FromArgb(125, 0 ,0 ,0));
                        g.FillRectangle(brush, rect);
                        g.DrawImage(zoomBitmap, rect.X + padding, rect.Y + padding);
                       
                        //放大镜装饰
                        using (var pen = new Pen(Color.FromArgb(125, 0, 255, 255), 5))
                        {
                            g.DrawLine(pen, rect.X + padding, rect.Y + padding + halfHeight, rect.Right - padding, rect.Y + padding + halfHeight);
                            g.DrawLine(pen, rect.X + padding + halfWidth, rect.Y + padding, rect.X + padding + halfWidth, rect.Y + padding + height);
                            pen.Color = Color.White;
                            pen.Width = 1;
                            g.DrawRectangle(pen, rect.X + 1, rect.Y + 1, rect.Width - padding, height + padding);

                            pen.Color = Color.Cyan;
                            g.DrawRectangle(pen, rect.Right - 12, rect.Bottom - 12, 10, 10);
                            g.DrawRectangle(pen, rect.X + halfWidth - 1, rect.Y + halfHeight - 1, 8, 8);
                        }
                        //显示数据
                        var str = $"{_pickedColor.A},{_pickedColor.R},{_pickedColor.G},{_pickedColor.B}{Environment.NewLine}" +
                                  $"0x{_pickedColor.A.ToString("X").PadLeft(2,'0')}{_pickedColor.R.ToString("X").PadLeft(2, '0')}{_pickedColor.G.ToString("X").PadLeft(2, '0')}{_pickedColor.B.ToString("X").PadLeft(2, '0')}";
                        g.DrawString(str, font, System.Drawing.Brushes.White, rect.X + padding, rect.Y + height + padding * 2);
                        brush.Color = _pickedColor;
                        g.FillRectangle(brush, rect.Right - 11, rect.Bottom - 11, 9, 9);//提取的颜色，右下角显示
                        g.FillRectangle(brush, rect.X + halfWidth, rect.Y + halfHeight, 7, 7);//提取的颜色，中间显示
                        brush.Dispose();
                    }
                }
            }
        }

        #endregion

    }
}
﻿using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using EasyImage.Behaviors;
using EasyImage.Config;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Serialization.Formatters.Binary;
using System.Windows.Interop;
using DealImage.Paste;
using EasyImage.Controls;
using NHotkey;
using NHotkey.Wpf;
using UnmanagedToolkit;
using WindowTemplate;
using ContextMenu = System.Windows.Controls.ContextMenu;
using Cursors = System.Windows.Input.Cursors;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MenuItem = System.Windows.Controls.MenuItem;
using Message = WindowTemplate.Message;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using Panel = System.Windows.Controls.Panel;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;

namespace EasyImage
{

    /// <summary>
    /// ImageWinodw.xaml 的交互逻辑
    /// </summary>
    public partial class ImageWindow
    {
        private UserConfig _userConfigution;
        private ControlManager _controlManager;
        private ClipboardMonitor _clipboardMonitor;
        private BitmapImage _cacheInternalBitmapSource;
        private UserControl _mainMenu;
        private int _addInternalImgCount;

        public ImageWindow()
        {
            InitializeComponent();
            ImageCanvas.Width = MainCanvas.Width = Width = SystemParameters.VirtualScreenWidth;
            ImageCanvas.Height = MainCanvas.Height = Height = SystemParameters.VirtualScreenHeight;

        }

        #region 重载方法

        /// <summary>
        /// 消息事件处理
        /// </summary>
        /// <param name="hwnd"></param>
        /// <param name="msg"></param>
        /// <param name="wParam"></param>
        /// <param name="lParam"></param>
        /// <param name="handled"></param>
        /// <returns></returns>
        IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == Win32.WmCopydata)
            {
                var copyDataStruct = (Win32.CopyDataStruct)Marshal.PtrToStructure(lParam, typeof(Win32.CopyDataStruct));
                var data = copyDataStruct.lpData;
                if (data == string.Empty)
                {
                    Extentions.ShowMessageBox("程序已运行");
                }
                else
                {
                    if (File.Exists(data))
                    {
                        Visibility = Visibility.Visible;
                        Activate();
                        if (_controlManager.StatusCodeChanged)
                        {
                            switch (IsSaveEasyIamgeToFile())
                            {
                                case ClickResult.LeftBtn:
                                    SaveEasyImageToFile(null, null);
                                    break;
                                case ClickResult.MiddleBtn:
                                    break;
                                case ClickResult.RightBtn:
                                case ClickResult.Close:
                                    return hwnd;
                            }
                        }
                        LoadEasyImageFromFile(data);
                    }
                }

            }
            return hwnd;
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var hwndSource = PresentationSource.FromVisual(this) as HwndSource;
            hwndSource?.AddHook(WndProc);
        }

        #endregion

        #region 主窗口事件

        private void WindowLoaded(object sender, RoutedEventArgs e)
        {
            #region 成员变量初始化

            ImageCanvas.Width = Width;
            ImageCanvas.Height = Height;
            _userConfigution = ((MainWindow) Owner).UserConfigution;
            _controlManager = new ControlManager(ImageCanvas);
            _controlManager.LoadPlugins(_userConfigution.AppSetting.PluginPath);
            _clipboardMonitor = new ClipboardMonitor();
            _clipboardMonitor.OnClipboardContentChanged += OnClipboardContentChanged;
            _addInternalImgCount = 0;

            #endregion

            #region 加载其它配置

            Win32.ChangeWindowMessageFilter(Win32.WmCopydata, 1);
            this.RemoveSystemMenuItems(Win32.SystemMenuItems.All); //去除窗口指定的系统菜单
            try
            {
                HotkeyManager.Current.AddOrReplace("GlobalPasteFromClipboard", Key.V,
                    ModifierKeys.Control | ModifierKeys.Alt, GlobalPasteFromClipboard);
                HotkeyManager.Current.AddOrReplace("GlobalAddCanvas", Key.N,
                    ModifierKeys.Control | ModifierKeys.Alt, GlobalAddCanvas);
            }
            catch(Exception ex)
            {
                App.Log.Error(ex.ToString());
                Extentions.ShowMessageBox("全局快捷键设置失败!");
            }

            InitMainMenu();

            var filePath = _userConfigution.WindowState.InitEasyImagePath;
            if (filePath != null)
            {
                LoadEasyImageFromFile(filePath);
            }

            #endregion

            //var timer = new DispatcherTimer(
            //            TimeSpan.FromMinutes(10),
            //            DispatcherPriority.ApplicationIdle,// Or DispatcherPriority.SystemIdle
            //            (s, e1) => MessageBox.Show("Timer"),
            //            Application.Current.Dispatcher);
        }

        private void WindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_controlManager.StatusCodeChanged)
            {
                switch (IsSaveEasyIamgeToFile())
                {
                    case ClickResult.LeftBtn:
                        SaveEasyImageToFile(null, null);
                        break;
                    case ClickResult.MiddleBtn:
                        break;
                    case ClickResult.RightBtn:
                    case ClickResult.Close:
                        e.Cancel = true;
                        return;
                }
            }
            if (!e.Cancel)
            {
                SaveCurrentState();
                Owner.Close();
            }
        }

        private void WindowHidden(object sender, ExecutedRoutedEventArgs e)
        {
            Visibility = Visibility.Hidden;
        }

        private void WindowKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Up || e.Key == Key.Right || e.Key == Key.Down || e.Key == Key.Left)
            {
                _controlManager.KeyMoveSelected(e.Key);
                if (_controlManager.MoveSpeed <= 10)
                {
                    _controlManager.MoveSpeed += 0.5;
                }
            }
            else if ((e.KeyboardDevice.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                switch (e.Key)
                {
                    case Key.S:
                        SaveEasyImageToFile(null,null);
                        break;
                    case Key.Z:
                        _controlManager.UnExecute();
                        break;
                    case Key.A:
                        _controlManager.SelectAll();
                        break;
                    case Key.C:
                        _controlManager.CopySelected();
                        break;
                    case Key.V:
                        PasteImagesFromClipboard(null, null);
                        break;
                    case Key.X:
                        _controlManager.ClipSelected();
                        break;
                    case Key.Y:
                        _controlManager.ReExecute();
                        break;
                    case Key.O:
                        LoadEasyImageFromFile(null, null);
                        break;
                }
            }
            else if (e.Key == Key.Delete)
            {
                _controlManager.RemoveSelected();
            }
        }

        private void WindowKeyUp(object sender, KeyEventArgs e)
        {
            _controlManager.MoveSpeed = 1.0;
        }

        private void WindowDrop(object sender, DragEventArgs e)
        {
            try
            {
                var curPosition = Mouse.GetPosition(null);
                var imageSources = ImagePaster.GetImageFromIDataObject(e.Data);
                if (imageSources.Count <= 0) return;
                _controlManager.SelectNone();
                var translate = new Point(curPosition.X - SystemParameters.VirtualScreenWidth / 2, curPosition.Y - SystemParameters.VirtualScreenHeight / 2);
                var controls = new List<ImageControl>(imageSources.Count);
                controls.AddRange(imageSources.Select(imageSource => PackageImageToControl(new AnimatedImage.AnimatedImage { Source = imageSource, Stretch = Stretch.Fill }, translate)));
                _controlManager.AddElements(controls);
            }
            catch (Exception ex)
            {
                App.Log.Error(ex.ToString());
                Extentions.ShowMessageBox("无效的粘贴!");
            }
        }

        private void WindowDragEnter(object sender, DragEventArgs e)
        {
            Activate();
        }

        #endregion

        #region 主菜单事件

        private void MainMenu_MouseDown(object sender, MouseButtonEventArgs e)
        {
            _controlManager.SelectNone();
        }

        private void MainMenu_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            var element = sender as UserControl;

            var menuItems = element?.ContextMenu?.Items?.OfType<MenuItem>().ToArray();
            if (menuItems == null) return;
            var menuItem = menuItems.SingleOrDefault(m => m.Tag.ToString() == "SaveAs");
            if (menuItem != null)
            {
                menuItem.Visibility = _userConfigution.WindowState.InitEasyImagePath == null
                    ? Visibility.Collapsed
                    : Visibility.Visible;
            }
            menuItem = menuItems.SingleOrDefault(m => m.Tag.ToString() == "Paste");
            if (menuItem != null)
            {
                menuItem.IsEnabled = ImagePaster.CanPasteImageFromClipboard();
            }
        }

        private void LoadEasyImageFromFile(object sender, RoutedEventArgs e)
        {
            if (_controlManager.StatusCodeChanged)
            {
                switch (IsSaveEasyIamgeToFile())
                {
                    case ClickResult.LeftBtn:
                        SaveEasyImageToFile(null, null);
                        break;
                    case ClickResult.MiddleBtn:
                        break;
                    case ClickResult.RightBtn:
                    case ClickResult.Close:
                        return;
                }
            }
            var dialog = new OpenFileDialog()
            {
                Filter = "EasyImage 元文件 (*.ei)|*.ei",
                CheckPathExists = true,
            };
            var showDialog = dialog.ShowDialog().GetValueOrDefault();
            if (!showDialog) return;
            LoadEasyImageFromFile(dialog.FileName);
        }

        private void AddImagesFromFile(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog()
            {
                Multiselect = true,
                CheckPathExists = true,
                Filter =
                    "所有图片 (*.ico;*.gif;*.jpg;*.jpeg;*.jfif;*.jpe;*.png;*.tif;*.tiff;*.bmp;*.dib;*.rle)|*.ico;*.gif;*.jpg;*.jpeg;*.jfif;*.jpe;*.png;*.tif;*.tiff;*.bmp;*.dib;*.rle"
                    + "|ICO 图标格式 (*.ico)|*.ico"
                    + "|GIF 可交换的图形格式 (*.gif)|*.gif"
                    + "|JPEG 文件交换格式 (*.jpg;*.jpeg;*.jfif;*.jpe)|*.jpg;*.jpeg;*.jfif;*.jpe"
                    + "|PNG 可移植网络图形格式 (*.png)|*.png"
                    + "|TIFF Tag 图像文件格式 (*.tif;*.tiff)|*.tif;*.tiff"
                    + "|设备无关位图 (*.bmp;*.dib;*.rle)|*.bmp;*.dib;*.rle"
            };

            var showDialog = dialog.ShowDialog().GetValueOrDefault();
            if (!showDialog) return;
            _controlManager.SelectNone();
            _controlManager.ContinuedAddCount = 0;
            var imageControls = new List<ImageControl>(dialog.FileNames.Length);
            foreach (var file in dialog.FileNames)
            {
                try
                {
                    imageControls.Add(PackageImageToControl(new AnimatedImage.AnimatedImage { Source = Extentions.GetBitmapImage(file), Stretch = Stretch.Fill }, new Point(0, 0)));
                }
                catch (Exception ex)
                {
                    App.Log.Error(ex.ToString());
                    Extentions.ShowMessageBox("不支持此格式的图片!");
                }
            }
            _controlManager.AddElements(imageControls);
        }

        private void AddImageFromInternal(object sender, RoutedEventArgs e)
        {
            var size = (int)_userConfigution.ImageSetting.InitMaxImgSize / 2;
            if (size < 1 || size > SystemParameters.VirtualScreenHeight)
            {
                size = (int)SystemParameters.VirtualScreenHeight / 2;
            }
            if (_cacheInternalBitmapSource == null || size != (int)_cacheInternalBitmapSource.Width)
            {
                var drawingVisual = new DrawingVisual();
                using (var context = drawingVisual.RenderOpen())
                {
                    context.DrawRectangle(Brushes.White, null, new Rect(0, 0, size, size));
                }
                drawingVisual.Opacity = 0.1;
                var renderBitmap = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
                renderBitmap.Render(drawingVisual);

                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(renderBitmap));
                var stream = new MemoryStream();
                encoder.Save(stream);
                if (_cacheInternalBitmapSource == null)
                {
                    _cacheInternalBitmapSource = new BitmapImage();
                    _cacheInternalBitmapSource.BeginInit();
                    _cacheInternalBitmapSource.StreamSource = stream;
                    _cacheInternalBitmapSource.EndInit();
                }
                else
                {
                    _cacheInternalBitmapSource.StreamSource = stream;
                }
            }
            _controlManager.SelectNone();
            _addInternalImgCount++;
            _controlManager.AddElement(PackageImageSourceToControl(_cacheInternalBitmapSource));
        }

        private void SaveEasyImageToFile(object sender, RoutedEventArgs e)
        {
            var filePath = _userConfigution.WindowState.InitEasyImagePath;
            if (filePath == null || !File.Exists(filePath))
            {
                var dialog = new SaveFileDialog
                {
                    CheckPathExists = true,
                    AddExtension = true,
                    FileName = "EasyImage1",
                    Filter = "EasyImage 元文件 (*.ei)|*.ei",
                    ValidateNames = true,
                };
                var showDialog = dialog.ShowDialog().GetValueOrDefault();
                if (!showDialog) return;
                filePath = dialog.FileName;
            }
            SaveEasyImageToFile(filePath);
        }

        private void SaveAsEasyImageToFile(object sender, RoutedEventArgs e)
        {
            var dialog = new SaveFileDialog
            {
                CheckPathExists = true,
                AddExtension = true,
                FileName = "EasyImage1",
                Filter = "EasyImage 元文件 (*.ei)|*.ei",
                ValidateNames = true,
            };
            var showDialog = dialog.ShowDialog().GetValueOrDefault();
            if (!showDialog) return;
            SaveEasyImageToFile(dialog.FileName);
        }

        private void PasteImagesFromClipboard(object sender, RoutedEventArgs e)
        {
            if (ImagePaster.CanInternalPasteFromClipboard())
            {
                var baseInfos = ImagePaster.GetInternalPasteDataFromClipboard() as List<ImageControlBaseInfo>;
                if (baseInfos != null)
                {
                    _controlManager.SelectNone();
                    _controlManager.ContinuedAddCount++;
                    _controlManager.AddElements(baseInfos.Select(m => PackageBaseInfoToControl(m, true)));
                    return;
                }
            }

            if (!ImagePaster.CanPasteImageFromClipboard()) return;
            try
            {
                var imageSources = ImagePaster.GetPasteImagesFromClipboard();
                if (imageSources.Count <= 0) return;
                _controlManager.SelectNone();
                _controlManager.ContinuedAddCount++;
                var translate = _userConfigution.ImageSetting.PasteMoveUnitDistace * _controlManager.ContinuedAddCount;
                var controls = new List<ImageControl>(imageSources.Count);
                controls.AddRange(imageSources.Select(imageSource => PackageImageToControl(new AnimatedImage.AnimatedImage { Source = imageSource, Stretch = Stretch.Fill }, new Point(translate, translate))));
                _controlManager.AddElements(controls);
            }
            catch (Exception ex)
            {
                App.Log.Error(ex.ToString());
                Extentions.ShowMessageBox("无效的粘贴!");
            }

        }

        private void CaptureScreen(object sender, RoutedEventArgs e)
        {
           
        }

        #endregion

        #region Private methods

        private void SaveCurrentState()
        {
            var mainMenuIconInfo = _userConfigution.ImageSetting.MainMenuInfo;
            mainMenuIconInfo.Width = _mainMenu.Width;
            mainMenuIconInfo.Height = _mainMenu.Height;
            var translate = _mainMenu.GetTransform<TranslateTransform>();
            mainMenuIconInfo.TranslateX = translate.X;
            mainMenuIconInfo.TranslateY = translate.Y;
        }

        private void InitMainMenu()
        {
            #region 初始化属性
            var mainMenuInfo = _userConfigution.ImageSetting.MainMenuInfo;

            _mainMenu = new UserControl
            {
                Content = GetMainMenuIcon(),
                Width = mainMenuInfo.Width,
                Height = mainMenuInfo.Height,
                ToolTip = "EasyImage主菜单",
                Cursor = Cursors.SizeAll,
            };
           
            var transformGroup = new TransformGroup();
            transformGroup.Children.Add(new ScaleTransform(1, 1));
            transformGroup.Children.Add(new RotateTransform(0));
            transformGroup.Children.Add(new TranslateTransform(mainMenuInfo.TranslateX, mainMenuInfo.TranslateY));
            _mainMenu.RenderTransform = transformGroup;

            var dragBehavior = new MouseDragElementBehavior<UserControl>
            {
                MoveableRange = new Rect(0, 0, SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight),
            };
            dragBehavior.Attach(_mainMenu);

            var autoHideBehavior = new AutoHideElementBehavior<UserControl>();
            autoHideBehavior.Attach(_mainMenu);

            #endregion

            #region 添加上下文菜单
            var contextMenu = new ContextMenu();
            var item = new MenuItem {Header = "新建", Tag = "New"};
            item.Click += AddImageFromInternal;
            contextMenu.Items.Add(item);

            item = new MenuItem { Header = "打开", Tag = "Open"};
            item.Click += LoadEasyImageFromFile;
            contextMenu.Items.Add(item);

            item = new MenuItem { Header = "添加", Tag = "Add"};
            item.Click += AddImagesFromFile;
            contextMenu.Items.Add(item);

            item = new MenuItem { Header = "保存", Tag = "Save"};
            item.Click += SaveEasyImageToFile;
            contextMenu.Items.Add(item);

            item = new MenuItem { Header = "另保存", Tag = "SaveAs"};
            item.Click += SaveAsEasyImageToFile;
            contextMenu.Items.Add(item);

            item = new MenuItem { Header = "粘贴", Tag = "Paste"};
            item.Click += PasteImagesFromClipboard;
            contextMenu.Items.Add(item);

            item = new MenuItem { Header = "截屏", Tag = "CaptureScreen"};
            item.Click += CaptureScreen;
            contextMenu.Items.Add(item);

            contextMenu.Items.Add(new Separator());//分割线

            item = new MenuItem { Header = "退出", Tag = "Exit"};
            item.Click += (sender, args) =>
            {
                Close();
            };
            contextMenu.Items.Add(item);

            _mainMenu.ContextMenu = contextMenu;
            #endregion

            #region 添加事件
            _mainMenu.MouseDown += MainMenu_MouseDown;
            _mainMenu.ContextMenuOpening += MainMenu_ContextMenuOpening;
            
            #endregion

            MainCanvas.Children.Add(_mainMenu);
        }

        private AnimatedImage.AnimatedImage GetMainMenuIcon()
        {
            AnimatedImage.AnimatedImage animatedImage;
            var path = _userConfigution.ImageSetting.MainMenuInfo.Path;
            if (!File.Exists(path))
            {
                path = Path.Combine(AppDomain.CurrentDomain.SetupInformation.ApplicationBase, path);
                if (!File.Exists(path))
                {
                    animatedImage = (AnimatedImage.AnimatedImage)Resources["MainMenuIcon"];
                    return animatedImage;
                }
            }
            try
            {
                animatedImage = new AnimatedImage.AnimatedImage
                {
                    Source = Extentions.GetBitmapImage(path),
                    Stretch = Stretch.Fill
                };
            }
            catch (Exception ex)
            {
                App.Log.Error(ex.ToString());
                animatedImage = (AnimatedImage.AnimatedImage)Resources["MainMenuIcon"];
            }

            return animatedImage;
        }

        private void LoadEasyImageFromFile(string filePath)
        {
            try
            {
                using (var sr = new StreamReader(filePath))
                {
                    var str = sr.ReadToEnd();
                    if (str.Length > 0)
                    {
                        using (var ms = new MemoryStream(Convert.FromBase64String(str)))
                        {
                            var baseInfos = new BinaryFormatter().Deserialize(ms) as List<ImageControlBaseInfo>;
                            if (baseInfos != null)
                            {
                                _controlManager.Clear();
                                _controlManager.Initialize(baseInfos.Select(m => PackageBaseInfoToControl(m, false)));
                            }
                        }
                    }
                }
                _userConfigution.WindowState.InitEasyImagePath = filePath;
                App.Log.InfoFormat("加载 EasyImage 元文件: {0}", filePath);
            }
            catch (Exception ex)
            {
                App.Log.Error(ex.ToString());
                Extentions.ShowMessageBox("无效的文件，打开失败!");
            }
        }

        private void SaveEasyImageToFile(string filePath)
        {
            try
            {
                var imageControls = ImageCanvas.Children.Cast<object>().OfType<ImageControl>().OrderBy(Panel.GetZIndex).ToList();
                if (imageControls.Count == 0)
                {
                    using (var stream = new FileStream(filePath, FileMode.OpenOrCreate, FileAccess.Write))
                    {
                        stream.SetLength(0);
                    }
                }
                else
                {
                    var baseInfos = imageControls.Select(m => new ImageControlBaseInfo(m)).ToList();
                    using (var ms = new MemoryStream())
                    {
                        new BinaryFormatter().Serialize(ms, baseInfos);
                        using (var sw = new StreamWriter(filePath))
                        {
                            sw.Write(Convert.ToBase64String(ms.ToArray()));
                        }
                    }
                }
                _controlManager.UpdateStatusCode();
                _userConfigution.WindowState.InitEasyImagePath = filePath;
            }
            catch (Exception ex)
            {
                App.Log.Error(ex.ToString());
                Extentions.ShowMessageBox("文件可能被占用,保存失败!");
            }
        }

        private ClickResult IsSaveEasyIamgeToFile()
        {
            var filePath = _userConfigution.WindowState.InitEasyImagePath;
            if (filePath == null || !File.Exists(filePath))
            {
                filePath = "EasyImage1";
            }
            filePath = filePath.Split('\\').Last();
            if (filePath.Length > 10)
            {
                filePath = filePath.Substring(0, 5) + "..." + filePath.Substring(filePath.Length - 5);
            }
            var msg = new Message($"是否保存对 {filePath} 的更改？", MessageBoxMode.ThreeMode);
            var msgWin = new MessageWindow(msg)
            {
                LeftBtnContent = "保存",
                MiddleBtnContent = "不保存",
                RightBtnContent = "取消",
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
            };
            msgWin.ShowDialog();
            return msg.Result;
        }

        private ImageControl PackageImageToControl(AnimatedImage.AnimatedImage image, Point translate)
        {
            var imageControl = new ImageControl(_controlManager);

            var width = imageControl.Width = image.Source.Width;
            var height = imageControl.Height = image.Source.Height;
            
            imageControl.Content = image;
            imageControl.Template = (ControlTemplate)Resources["MoveResizeRotateTemplate"];

            //调整大小
            var initMaxImgSize = _userConfigution.ImageSetting.InitMaxImgSize;
            if (width > height && width > initMaxImgSize)
            {
                imageControl.Height = initMaxImgSize * height / width;
                imageControl.Width = initMaxImgSize;
            }
            else if (height > width && height > initMaxImgSize)
            {
                imageControl.Height = initMaxImgSize;
                imageControl.Width = initMaxImgSize * width / height;
            }

            var transformGroup = new TransformGroup();
            transformGroup.Children.Add(new ScaleTransform(1, 1));
            transformGroup.Children.Add(new RotateTransform(0));
            transformGroup.Children.Add(new TranslateTransform((SystemParameters.VirtualScreenWidth - imageControl.Width) / 2 + translate.X, (SystemParameters.VirtualScreenHeight - imageControl.Height) / 2 + translate.Y));
            imageControl.RenderTransform = transformGroup;
            
            return imageControl;
        }

        private ImageControl PackageImageSourceToControl(ImageSource imageSource)
        {
            var animatedImage = new AnimatedImage.AnimatedImage { Source = imageSource, Stretch = Stretch.Fill };
            var imageControl = new ImageControl(_controlManager)
            {
                IsLockAspect = false,
                Width = imageSource.Width,
                Height = imageSource.Height,
                Content = animatedImage,
                Template = (ControlTemplate)Resources["MoveResizeRotateTemplate"],
            };

            var transformGroup = new TransformGroup();
            transformGroup.Children.Add(new ScaleTransform(1, 1));
            transformGroup.Children.Add(new RotateTransform(0));
            double moveX = 0, moveY = 0;
            switch (_addInternalImgCount)
            {
                case 1:
                    break;
                case 2:
                    moveX = -imageSource.Width / 2;
                    moveY = -imageSource.Height / 2;
                    break;
                case 3:
                    moveX = imageSource.Width / 2;
                    moveY = -imageSource.Height / 2;
                    break;
                case 4:
                    moveX = imageSource.Width / 2;
                    moveY = imageSource.Height / 2;
                    break;
                case 5:
                    moveX = -imageSource.Width / 2;
                    moveY = imageSource.Height / 2;
                    _addInternalImgCount = 0;
                    break;
            }
            transformGroup.Children.Add(new TranslateTransform((SystemParameters.VirtualScreenWidth - imageControl.Width) / 2 + moveX, (SystemParameters.VirtualScreenHeight - imageControl.Height) / 2 + moveY));
            imageControl.RenderTransform = transformGroup;

            return imageControl;
        }

        private ImageControl PackageBaseInfoToControl(ImageControlBaseInfo baseInfo, bool isMove)
        {
            //内存优化
            var existedImageControl = ImageCanvas.Children.Cast<ImageControl>().FirstOrDefault(m => m.Id == baseInfo.Id);
            AnimatedImage.AnimatedImage animatedImage;
            Guid guid;
            if (existedImageControl == null)
            {
                animatedImage =  new AnimatedImage.AnimatedImage { Source = baseInfo.ImageSource, Stretch = Stretch.Fill };
                guid = Guid.Parse(baseInfo.Id);
            }
            else
            {
                animatedImage = new AnimatedImage.AnimatedImage
                {
                    Source = (existedImageControl.Content as AnimatedImage.AnimatedImage)?.Source,
                    Stretch = Stretch.Fill
                };
                guid = Guid.NewGuid();
            }
            
            var imageControl = new ImageControl(_controlManager, guid)
            {
                IsLockAspect = baseInfo.FreeResize,
                Width = baseInfo.Width,
                Height = baseInfo.Height,
                Content = animatedImage,
                Template = (ControlTemplate) Resources["MoveResizeRotateTemplate"],
                RenderTransform = baseInfo.RenderTransform,
            };
            if (isMove)
            {
                var translateTransform = imageControl.GetTransform<TranslateTransform>();
                translateTransform.X += _userConfigution.ImageSetting.PasteMoveUnitDistace * _controlManager.ContinuedAddCount;
                translateTransform.Y += _userConfigution.ImageSetting.PasteMoveUnitDistace * _controlManager.ContinuedAddCount;
            }

            return imageControl;
        }

        #region 全局事件

        private void GlobalPasteFromClipboard(object sender, HotkeyEventArgs e)
        {
            PasteImagesFromClipboard(null, null);
        }

        private void GlobalAddCanvas(object sender, HotkeyEventArgs e)
        {
            AddImageFromInternal(null, null);
        }

        private void OnClipboardContentChanged(object sender, EventArgs e)
        {
            _controlManager.ContinuedAddCount = 0;
        }



        #endregion

        #endregion

        
    }

}

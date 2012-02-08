using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.Kinect;
using Coding4Fun.Kinect.Wpf;
using System.Timers;
using System.Windows.Threading;

namespace KinectTest
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        Timer rollTimer;
        public MainWindow()
        {
            InitializeComponent();

            // Timer
            rollTimer = new Timer();
            rollTimer.Elapsed += new ElapsedEventHandler(rollMenu);
            rollTimer.Interval = 2000;
        }

        // Distance to camera
        const float MaxDepthDistance = 4000;
        const float MinDepthDistance = 850;
        const float MaxDepthDistanceOffset = MaxDepthDistance - MinDepthDistance;
        int greenIndex = 0;

        delegate void UpdateTheUI();

        bool closing = false;
        bool rolling = false;

        bool changeColor = false;

        const int skeletonCount = 6;
        Skeleton[] allSkeletons = new Skeleton[skeletonCount];

        double clickLeftBorder;
        double rollRightBorder;

        List<Rectangle> menu = new List<Rectangle>();

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            kinectSensorChooser1.KinectSensorChanged += new DependencyPropertyChangedEventHandler(kinectSensorChooser1_KinectSensorChanged);

            clickLeftBorder = Canvas.GetLeft(click);

            rollRightBorder = Canvas.GetLeft(roll) + roll.ActualWidth;

            Console.WriteLine(clickLeftBorder);

            menu.Add(menu1);
            menu.Add(menu2);
            menu.Add(menu3);

        }

        void kinectSensorChooser1_KinectSensorChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            KinectSensor oldSensor = (KinectSensor)e.OldValue;
            StopKinect(oldSensor);

            KinectSensor newSensor = (KinectSensor)e.NewValue;

            if (newSensor == null)
            {
                return;
            }

            // Skelly
            newSensor.SkeletonStream.Enable();
            // Trigger on event allFramesReady
            newSensor.AllFramesReady += new EventHandler<AllFramesReadyEventArgs>(newSensor_AllFramesReady);

            // Depth cam
            newSensor.DepthStream.Enable(DepthImageFormat.Resolution640x480Fps30);

            // Color camera
            newSensor.ColorStream.Enable(ColorImageFormat.RgbResolution640x480Fps30);
        
            try
            {
                newSensor.Start();
            }
            catch (System.IO.IOException)
            {
                kinectSensorChooser1.AppConflictOccurred();
            }
        }

        void newSensor_AllFramesReady(object sender, AllFramesReadyEventArgs e)
        {
            if (closing)
            {
                return;
            }

            // Get a skeleton
            Skeleton first = GetFirstSkeleton(e);

            if (first == null)
            {
                return;
            }

            GetCameraPoint(first, e);
        }

        private void GetCameraPoint(Skeleton first, AllFramesReadyEventArgs e)
        {
            using (DepthImageFrame depth = e.OpenDepthImageFrame())
            {
                if (depth == null ||
                    kinectSensorChooser1.Kinect == null)
                {
                    return;
                }

                // Map joint to location
                // Head
                DepthImagePoint headDeapthPoint =
                    depth.MapFromSkeletonPoint(first.Joints[JointType.Head].Position);

                //Left hand
                DepthImagePoint leftDeapthPoint =
                    depth.MapFromSkeletonPoint(first.Joints[JointType.HandLeft].Position);

                //Right hand
                DepthImagePoint rightDeapthPoint =
                    depth.MapFromSkeletonPoint(first.Joints[JointType.HandRight].Position);

                // Map a depth point to a point on the color image
                // Head
                ColorImagePoint headColorPoint =
                    depth.MapToColorImagePoint(headDeapthPoint.X, headDeapthPoint.Y, ColorImageFormat.RgbResolution640x480Fps30);

                // Left hand
                ColorImagePoint leftColorPoint =
                    depth.MapToColorImagePoint(leftDeapthPoint.X, leftDeapthPoint.Y, ColorImageFormat.RgbResolution640x480Fps30);

                // Right hand
                ColorImagePoint rightColorPoint =
                    depth.MapToColorImagePoint(rightDeapthPoint.X, rightDeapthPoint.Y, ColorImageFormat.RgbResolution640x480Fps30);

                Canvas.SetTop(headEllipse, 0.0);

                CameraPosition(headEllipse, headColorPoint);
                CameraPosition(leftEllipse, leftColorPoint);
                CameraPosition(rightEllipse, rightColorPoint);
            }
        }

        private void CameraPosition(FrameworkElement element, ColorImagePoint point)
        {
            Canvas.SetLeft(element, point.X - element.Width / 2);
            Canvas.SetTop(element, point.Y - element.Height / 2);

            // Check if you're choosing any of the choices
            if (element.Name.Equals("rightEllipse"))
            {
                if(Canvas.GetLeft(element) > clickLeftBorder)
                {
                    Console.WriteLine("You clicked");
                    switch (greenIndex)
                    { 
                        case 0:
                            clickLabel.Content = "Bottom box clicked";
                            break;
                        case 1:
                            clickLabel.Content = "Top box clicked";
                            break;
                        case 2:
                            clickLabel.Content = "Middle box clicked";
                            break;
                    }
                }
            }
            else if (element.Name.Equals("leftEllipse"))
            {
                if (Canvas.GetLeft(element) < rollRightBorder)
                {
                    Console.WriteLine("You be rollin");

                    if (rolling == false)
                    {
                        rollTimer.Start();
                        rolling = true;
                    }
                }
                else 
                {
                    if(rolling == true)
                    {
                        rollTimer.Stop();
                        rolling = false;
                    }
                }
            }
        }

        private Skeleton GetFirstSkeleton(AllFramesReadyEventArgs e)
        {
            using (SkeletonFrame skeletonFrameData = e.OpenSkeletonFrame())
            {
                if (skeletonFrameData == null)
                {
                    return null;
                }

                skeletonFrameData.CopySkeletonDataTo(allSkeletons);

                Skeleton first = (from s in allSkeletons
                                    where s.TrackingState == SkeletonTrackingState.Tracked
                                    select s).FirstOrDefault();
                return first;
            }
        }

        void StopKinect(KinectSensor sensor)
        {
            if(sensor != null)
            {
                sensor.Stop();
                sensor.AudioSource.Stop();
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            StopKinect(kinectSensorChooser1.Kinect);
        }

        public static byte CalculateIntensityFromDepth(int distance)
        {
            return (byte)(255 - (255 * Math.Max(distance - MinDepthDistance, 0)
                / (MaxDepthDistanceOffset)));
        }

        public void rollMenu(object source, ElapsedEventArgs e )
        {
            this.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (UpdateTheUI)delegate()
            {
                menu.ElementAt(greenIndex).Fill = new SolidColorBrush(Colors.Red);
                greenIndex = (greenIndex + 1) % menu.Count;
                menu.ElementAt(greenIndex).Fill = new SolidColorBrush(Colors.Green);
            } );
        }
    }
}

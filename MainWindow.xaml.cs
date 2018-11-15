using System.Windows;
using System.Windows.Input;
using System;
using System.Runtime.InteropServices;
using System.Windows.Threading;
//using System.Windows.PresentationSource;
using System.Windows.Media;
//using System.Windows.Forms;
using Microsoft.Kinect;
using Microsoft.Kinect.VisualGestureBuilder;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using WindowsInput;

namespace Microsoft.Samples.Kinect.DiscreteGestureBasics
{
    public partial class MainWindow : Window
    {
        //KinectControl kinectCtrl = new KinectControl();
        /// <summary>
        /// Active Kinect sensor
        /// </summary>
        KinectSensor sensor;
        /// <summary>
        /// Reader for body frames
        /// </summary>
        BodyFrameReader bodyFrameReader;
        /// <summary>
        /// Array for the bodies
        /// </summary>
        /// <summary> List of gesture detectors, there will be one detector created for each potential body (max of 6) </summary>
        private List<GestureDetector> gestureDetectorList = null;

        private Body[] bodies = null;
        /// <summary>
        /// Screen width and height for determining the exact mouse sensitivity
        /// </summary>
        int screenWidth, screenHeight;

        /// <summary>
        /// timer for pause-to-click feature
        /// </summary>
        DispatcherTimer timer = new DispatcherTimer();

        /// <summary>
        /// How far the cursor move according to your hand's movement
        /// </summary>
        public float mouseSensitivity = MOUSE_SENSITIVITY;

        /// <summary>
        /// Time required as a pause-clicking
        /// </summary>
        public float timeRequired = TIME_REQUIRED;
        /// <summary>
        /// The radius range your hand move inside a circle for [timeRequired] seconds would be regarded as a pause-clicking
        /// </summary>
        public float pauseThresold = PAUSE_THRESOLD;
        /// <summary>
        /// Decide if the user need to do clicks or only move the cursor
        /// </summary>
        public static bool doClick = DO_CLICK;
        /// <summary>
        /// Use Grip gesture to click or not
        /// </summary>
       /// public bool useGripGesture = USE_GRIP_GESTURE;
        /// <summary>
        /// Value 0 - 0.95f, the larger it is, the smoother the cursor would move
        /// </summary>
        public float cursorSmoothing = CURSOR_SMOOTHING;

        // Default values
        public const float MOUSE_SENSITIVITY = 3.5f;
        public const float TIME_REQUIRED = 2f;
        public const float PAUSE_THRESOLD = 60f;
        public const bool DO_CLICK = true;
        ///public const bool USE_GRIP_GESTURE = true;
        public const float CURSOR_SMOOTHING = 0.2f;

        /// <summary>
        /// Determine if we have tracked the hand and used it to move the cursor,
        /// If false, meaning the user may not lift their hands, we don't get the last hand position and some actions like pause-to-click won't be executed.
        /// </summary>
        bool alreadyTrackedPos = false;

        /// <summary>
        /// for storing the time passed for pause-to-click
        /// </summary>
        float timeCount = 0;
        /// <summary>
        /// For storing last cursor position
        /// </summary>
        Point lastCurPos = new Point(0, 0);

        /// <summary>
        /// If true, user did a left hand Grip gesture
        /// </summary>
        bool wasLeftGrip = false;
        /// <summary>
        /// If true, user did a right hand Grip gesture
        /// </summary>
        bool wasRightGrip = false;


        public MainWindow()
        {

            // get Active Kinect Sensor
            this.sensor = KinectSensor.GetDefault();

            // open the sensor
            this.sensor.Open();

            // open the reader for the body frames
            this.bodyFrameReader = sensor.BodyFrameSource.OpenReader();
            this.bodyFrameReader.FrameArrived += bodyFrameReader_FrameArrived;

            // initialize the gesture detection objects for our gestures
            this.gestureDetectorList = new List<GestureDetector>();

            this.InitializeComponent();

            // get screen with and height
            screenWidth = (int)SystemParameters.PrimaryScreenWidth;
            screenHeight = (int)SystemParameters.PrimaryScreenHeight;

            // set up timer, execute every 0.1s
            timer.Interval = new TimeSpan(0, 0, 0, 0, 100);
            timer.Tick += new EventHandler(Timer_Tick);
            timer.Start();

            int maxBodies = this.sensor.BodyFrameSource.BodyCount;
            for (int i = 0; i < maxBodies; ++i)
            {
                GestureResultView result = new GestureResultView(i, false, false, 0.0f,-1.0f,false,false,false);
                GestureDetector detector = new GestureDetector(this.sensor, result);
                this.gestureDetectorList.Add(detector);

            }

        }

        /// <summary>
        /// Pause to click timer
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void Timer_Tick(object sender, EventArgs e)
        {
            if (!doClick ) return;

            if (!alreadyTrackedPos)
            {
                timeCount = 0;
                return;
            }

            Point curPos = MouseControl.GetCursorPosition();
            foreach (Body body in this.bodies)
            {
                if ((lastCurPos - curPos).Length < pauseThresold)
                {
                    if ((timeCount += 0.1f) > timeRequired)
                    {
                        //MouseControl.MouseLeftDown();
                        //MouseControl.MouseLeftUp();


                        if (body.HandRightState == HandState.Lasso )
                        {


                            MouseControl.DoMouseClick1();


                        }
                        
                        /*else if (body.HandRightState == HandState.Open && body.HandLeftState == HandState.Open )
                        {
                            MouseControl.DoScroll();
                        }*/
                        else if (body.HandRightState == HandState.Open && body.HandLeftState == HandState.Open)
                        {
                            MouseControl.DoPause();
                        }
                      else if (body.HandRightState == HandState.Open && body.HandLeftState == HandState.Closed)
                        {
                            MouseControl.DoMouseClick();
                            MouseControl.DoMouseClick();
                        }
                        else if (body.HandRightState == HandState.Open && body.HandLeftState == HandState.Lasso)
                        {
                          
                            MouseControl.DoMouseClick();
                        }
                     
                        timeCount = 0;
                    }

                }

                else
                {
                    timeCount = 0;
                }

                lastCurPos = curPos;
            }
        }

        /// <summary>
        /// Read body frames
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void bodyFrameReader_FrameArrived(object sender, BodyFrameArrivedEventArgs e)
        {
            bool dataReceived = false;

            using (BodyFrame bodyFrame = e.FrameReference.AcquireFrame())
            {
                if (bodyFrame != null)
                {
                    if (this.bodies == null)
                    {
                        this.bodies = new Body[bodyFrame.BodyCount];
                    }

                    // The first time GetAndRefreshBodyData is called, Kinect will allocate each Body in the array.
                    // As long as those body objects are not disposed and not set to null in the array,
                    // those body objects will be re-used.
                    bodyFrame.GetAndRefreshBodyData(this.bodies);
                    dataReceived = true;
                }
            }

            if (!dataReceived)
            {
                alreadyTrackedPos = false;
                return;
            }

            if (this.bodies != null)
            {
                // loop through all bodies to see if any of the gesture detectors need to be updated
                int maxBodies = this.sensor.BodyFrameSource.BodyCount;
                for (int i = 0; i < maxBodies; ++i)
                {
                    Body body = this.bodies[i];
                    ulong trackingId = body.TrackingId;

                    // if the current body TrackingId changed, update the corresponding gesture detector with the new value
                    if (trackingId != this.gestureDetectorList[i].TrackingId)
                    {
                        this.gestureDetectorList[i].TrackingId = trackingId;

                        // if the current body is tracked, unpause its detector to get VisualGestureBuilderFrameArrived events
                        // if the current body is not tracked, pause its detector so we don't waste resources trying to get invalid gesture results
                        this.gestureDetectorList[i].IsPaused = trackingId == 0;
                    }
                }
            }
            foreach (Body body in this.bodies)
            {

                // get first tracked body only, notice there's a break below.
                if (body.IsTracked)
                {
                    // get various skeletal positions
                    CameraSpacePoint handLeft = body.Joints[JointType.HandLeft].Position;
                    CameraSpacePoint handRight = body.Joints[JointType.HandRight].Position;
                    CameraSpacePoint spineBase = body.Joints[JointType.SpineBase].Position;
                    if (handRight.Z - spineBase.Z < -0.15f)
                    {
                         if ((handRight.Y - spineBase.Y >= 0.9) && (body.HandRightState == HandState.Open))
                        {
                           
                            MouseControl.Vol_Up();
                        //    System.Threading.Thread.Sleep(2500);
                        }
                          else if ((handRight.Y - spineBase.Y <= 0.2f) && (body.HandRightState == HandState.Open))
                         {
                             MouseControl.Vol_Down();
                              //   System.Threading.Thread.Sleep(2500);
                         }
                         else if( (handRight.X - handLeft.X <= 0.25f) && (body.HandRightState == HandState.Open && body.HandLeftState == HandState.Open) )
                         {
                            
                             
                                 MouseControl.DoScroll();
                             
                         }
                         /*else if ((handRight.X - handLeft.X <= 0.45f) && (handRight.X - handLeft.X >= 0.25f) && (body.HandRightState == HandState.Open && body.HandLeftState == HandState.Open))
                         {

                             MouseControl.DoPause();

                         }*/
                         /*else if (handRight.X - handLeft.X >= 0.2f)
                         {
                             if (body.HandRightState == HandState.Open && body.HandLeftState == HandState.Open)
                             {
                                 MouseControl.DoPause();
                             }
                         }*/
                       /* if ((handRight.X - spineBase.X >= 0.5f) && (body.HandRightState == HandState.Open))
                        {
                            System.Windows.Forms.SendKeys.SendWait("{Left}");
                        }

                        else if ((handLeft.X - spineBase.X <= 0.5f) && (body.HandLeftState == HandState.Open))
                        {
                            System.Windows.Forms.SendKeys.SendWait("{Right}");
                        }*/

                       if (handRight.Z - spineBase.Z < -0.15f) // if right hand lift up
                        {
                            /* hand x calculated by this. we don't use shoulder right as a reference cause the shoulder right
                             * is usually behind the lift right hand, and the position would be inferred and unstable.
                             * because the spine base is on the left of right hand, we plus 0.05f to make it closer to the right. */
                            float x = handRight.X - spineBase.X + 0.05f;
                            /* hand y calculated by this. ss spine base is way lower than right hand, we plus 0.51f to make it
                             * higer, the value 0.51f is worked out by testing for a several times, you can set it as another one you like. */
                            float y = spineBase.Y - handRight.Y + 0.51f;
                            // get current cursor position
                            Point curPos = MouseControl.GetCursorPosition();
                            // smoothing for using should be 0 - 0.95f. The way we smooth the cusor is: oldPos + (newPos - oldPos) * smoothValue
                            float smoothing = 1 - cursorSmoothing;
                            // set cursor position
                            MouseControl.SetCursorPos((int)(curPos.X + (x * mouseSensitivity * screenWidth - curPos.X) * smoothing), (int)(curPos.Y + ((y + 0.25f) * mouseSensitivity * screenHeight - curPos.Y) * smoothing));

                            alreadyTrackedPos = true;

                            // Grip gesture
                           if (doClick)
                            {
                                if (body.HandRightState == HandState.Closed)
                                {
                                    if (!wasRightGrip)
                                    {
                                        MouseControl.MouseLeftDown();
                                        wasRightGrip = true;
                                    }
                                }

                                else if (body.HandRightState == HandState.Open)
                                {
                                    if (wasRightGrip)
                                    {
                                        MouseControl.MouseLeftUp();
                                        wasRightGrip = false;
                                    }
                                }
                            }
                        }
                    }
                   /* else if (handLeft.Z - spineBase.Z < -0.15f) // if left hand lift forward
                    {
                        float x = handLeft.X - spineBase.X + 0.3f;
                        float y = spineBase.Y - handLeft.Y + 0.51f;
                        Point curPos = MouseControl.GetCursorPosition();
                        float smoothing = 1 - cursorSmoothing;
                        MouseControl.SetCursorPos((int)(curPos.X + (x * mouseSensitivity * screenWidth - curPos.X) * smoothing), (int)(curPos.Y + ((y + 0.25f) * mouseSensitivity * screenHeight - curPos.Y) * smoothing));
                        alreadyTrackedPos = true;

                        if (doClick )
                        {
                            if (body.HandLeftState == HandState.Closed)
                            {
                                if (!wasLeftGrip)
                                {
                                    MouseControl.MouseLeftDown();
                                    wasLeftGrip = true;
                                }
                            }
                            else if (body.HandLeftState == HandState.Open)
                            {
                                if (wasLeftGrip)
                                {
                                    MouseControl.MouseLeftUp();
                                    wasLeftGrip = false;
                                }
                            }
                        }
                    }*/
                    else
                    {
                        wasLeftGrip = true;
                        wasRightGrip = true;
                        alreadyTrackedPos = false;
                    }

                    // get first tracked body only
                    break;
                }
            }
        }




        private void MouseSensitivity_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (MouseSensitivity.IsLoaded)
            {
                mouseSensitivity = (float)MouseSensitivity.Value;
                txtMouseSensitivity.Text = mouseSensitivity.ToString("f2");

                Properties.Settings.Default.MouseSensitivity = mouseSensitivity;
                Properties.Settings.Default.Save();
            }
        }

        private void PauseToClickTime_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (PauseToClickTime.IsLoaded)
            {
                timeRequired = (float)PauseToClickTime.Value;
                txtTimeRequired.Text = timeRequired.ToString("f2");

                Properties.Settings.Default.PauseToClickTime = timeRequired;
                Properties.Settings.Default.Save();
            }
        }

        private void txtMouseSensitivity_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                float v;
                if (float.TryParse(txtMouseSensitivity.Text, out v))
   {
                    MouseSensitivity.Value = v;
                    mouseSensitivity = (float)MouseSensitivity.Value;
                }
            }
        }

        private void txtTimeRequired_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                float v;
                if (float.TryParse(txtTimeRequired.Text, out v))
                {
                    PauseToClickTime.Value = v;
                    timeRequired = (float)PauseToClickTime.Value;
                }
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            MouseSensitivity.Value = Properties.Settings.Default.MouseSensitivity;
            PauseToClickTime.Value = Properties.Settings.Default.PauseToClickTime;
            PauseThresold.Value = Properties.Settings.Default.PauseThresold;
            chkNoClick.IsChecked = !Properties.Settings.Default.DoClick;
            CursorSmoothing.Value = Properties.Settings.Default.CursorSmoothing;
            

        }

        private void PauseThresold_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (PauseThresold.IsLoaded)
            {
                pauseThresold = (float)PauseThresold.Value;
                txtPauseThresold.Text = pauseThresold.ToString("f2");

                Properties.Settings.Default.PauseThresold = pauseThresold;
                Properties.Settings.Default.Save();
            }
        }

        private void txtPauseThresold_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                float v;
                if (float.TryParse(txtPauseThresold.Text, out v))
                {
                    PauseThresold.Value = v;
                    timeRequired = (float)PauseThresold.Value;
                }
            }
        }

        private void btnDefault_Click(object sender, RoutedEventArgs e)
        {
            MouseSensitivity.Value = KinectControl.MOUSE_SENSITIVITY;
            PauseToClickTime.Value = KinectControl.TIME_REQUIRED;
            PauseThresold.Value = KinectControl.PAUSE_THRESOLD;
            CursorSmoothing.Value = KinectControl.CURSOR_SMOOTHING;

            chkNoClick.IsChecked = !KinectControl.DO_CLICK;
            ///rdiGrip.IsChecked = KinectControl.USE_GRIP_GESTURE;
        }

        private void chkNoClick_Checked(object sender, RoutedEventArgs e)
        {
            chkNoClickChange();
        }


        public void chkNoClickChange()
        {
            doClick = !chkNoClick.IsChecked.Value;
            Properties.Settings.Default.DoClick = doClick;
            Properties.Settings.Default.Save();
        }

        private void chkNoClick_Unchecked(object sender, RoutedEventArgs e)
        {
            chkNoClickChange();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {

            if (timer != null)
            {
                timer.Stop();
                timer = null;
            }

            if (this.bodyFrameReader != null)
            {
                // BodyFrameReader is IDisposable
                this.bodyFrameReader.FrameArrived -= this.bodyFrameReader_FrameArrived;
                this.bodyFrameReader.Dispose();
                this.bodyFrameReader = null;
            }

            if (this.gestureDetectorList != null)
            {
                // The GestureDetector contains disposable members (VisualGestureBuilderFrameSource and VisualGestureBuilderFrameReader)
                foreach (GestureDetector detector in this.gestureDetectorList)
                {
                    detector.Dispose();
                }

                this.gestureDetectorList.Clear();
                this.gestureDetectorList = null;
            }

            if (this.sensor != null)
            {
                this.sensor.Close();
                this.sensor = null;
            }
        }

        /*public void rdiGripGestureChange()
        {
            ///useGripGesture = rdiGrip.IsChecked.Value;
            Properties.Settings.Default.GripGesture = useGripGesture;
            Properties.Settings.Default.Save();
        }*/

        /*private void rdiGrip_Checked(object sender, RoutedEventArgs e)
        {
            rdiGripGestureChange();
        }*/

        /*private void rdiPause_Checked(object sender, RoutedEventArgs e)
        {
            rdiGripGestureChange();
        }*/

        private void CursorSmoothing_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (CursorSmoothing.IsLoaded)
            {
                cursorSmoothing = (float)CursorSmoothing.Value;
                txtCursorSmoothing.Text = cursorSmoothing.ToString("f2");

                Properties.Settings.Default.CursorSmoothing = cursorSmoothing;
                Properties.Settings.Default.Save();
            }
        }


    }


}

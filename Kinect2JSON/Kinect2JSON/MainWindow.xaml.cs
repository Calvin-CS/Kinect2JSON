/*
 * Authors: Isaac Zylstra and Victor Norman @ Calvin College, Grand Rapids, MI.
 * Contact: vtn2@calvin.edu
 */

using Fleck;
using Microsoft.Kinect;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Kinect2JSON
{

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        //The list of websockets that this server transmits to
        static List<IWebSocketConnection> _clients = new List<IWebSocketConnection>();

        //The kinect sensor, the frame reader, and the mapper (to map the skeleton to the display window)
        private KinectSensor kinectSensor = null;
        private MultiSourceFrameReader multiSourceFrameReader = null;
        private CoordinateMapper coordinateMapper = null;

        //The list of bones and colors (for the skeleton display)
        private List<Tuple<JointType, JointType>> bones;
        private List<Pen> bodyColors;

        //the width and height of the joint space
        private int displayWidth;
        private int displayHeight;
        
        //The body array that is read from the bodyFrame (may contain null or untracked bodies)
        private Body[] bodies = null;
        //The body array that only contains tracked bodies
        private Body[] validBodies = null;
        // a boolean checking if the current bodyFrame contains bodies
        bool dataReceived = false;

        //The color bitmap to be displayed
        private WriteableBitmap colorBitmap = null;

        /// <summary>
        /// Radius of drawn hand circles
        /// </summary>
        private const double HandSize = 30;

        /// <summary>
        /// Thickness of drawn joint lines
        /// </summary>
        private const double JointThickness = 3;

        /// <summary>
        /// Thickness of clip edge rectangles
        /// </summary>
        private const double ClipBoundsThickness = 10;

        /// <summary>
        /// Constant for clamping Z values of camera space points from being negative
        /// </summary>
        private const float InferredZPositionClamp = 0.1f;


        /// <summary>
        /// Brush used for drawing hands that are currently tracked as closed
        /// </summary>
        private readonly Brush handClosedBrush = new SolidColorBrush(Color.FromArgb(128, 255, 0, 0));

        /// <summary>
        /// Brush used for drawing hands that are currently tracked as opened
        /// </summary>
        private readonly Brush handOpenBrush = new SolidColorBrush(Color.FromArgb(128, 0, 255, 0));

        /// <summary>
        /// Brush used for drawing hands that are currently tracked as in lasso (pointer) position
        /// </summary>
        private readonly Brush handLassoBrush = new SolidColorBrush(Color.FromArgb(128, 0, 0, 255));

        /// <summary>
        /// Brush used for drawing joints that are currently tracked
        /// </summary>
        private readonly Brush trackedJointBrush = new SolidColorBrush(Color.FromArgb(255, 68, 192, 68));

        /// <summary>
        /// Brush used for drawing joints that are currently inferred
        /// </summary>        
        private readonly Brush inferredJointBrush = Brushes.Yellow;

        /// <summary>
        /// Pen used for drawing bones that are currently inferred
        /// </summary>        
        private readonly Pen inferredBonePen = new Pen(Brushes.Gray, 1);

        /// <summary>
        /// Drawing group for body rendering output
        /// </summary>
        private DrawingGroup drawingGroup;

        /// <summary>
        /// Drawing image that we will display
        /// </summary>
        private DrawingImage imageSource2;

        //The color display
        public ImageSource ImageSource1
        {
            get
            {
                return this.colorBitmap;
            }
        }

        //The body display
        public ImageSource ImageSource2
        {
            get
            {
                return this.imageSource2;
            }
        }

        //Event handle for property changed which handles the status text and both display windows
        public event PropertyChangedEventHandler PropertyChanged;

        //The code that updates the status text based on whether the kinect is running.
        //Changed by Sensor_IsAvailableChanged
        private string kinectStatusText = null;
        public string KinectStatusText
        {
            get { return this.kinectStatusText; }
            set
            {
                if (this.kinectStatusText != value)
                {
                    this.kinectStatusText = value;
                    if (this.PropertyChanged != null)
                    {
                        this.PropertyChanged(this, new
                            PropertyChangedEventArgs("KinectStatusText"));
                    }
                }
            }
        }

        //Boolean stating whether the server has started
        public bool publicStarted = false;
        //The code that updates the status text based on whether the public connection has started.
        //Changed in InitializePublicConnection
        private string publicConnectionStatusText = "hello!";
        public string PublicConnectionStatusText
        {
            get { return this.publicConnectionStatusText; }
            set
            {
                if (this.publicConnectionStatusText != value)
                {
                    this.publicConnectionStatusText = value;
                    if (this.PropertyChanged != null)
                    {
                        this.PropertyChanged(this, new
                            PropertyChangedEventArgs("PublicConnectionStatusText"));
                    }
                }
            }
        }

        //runs the main functions, divided for clarity
        public MainWindow()
        {
            InitializeKinect();
            SetupColorDisplay();
            SetupBodyJointsDisplay();
            InitializeLocalConnection();
            //InitializePublicConnection();

            InitializeComponent();
        }
        /*
         * starting up the local websocket server
         * we use 127.0.0.1:8181 because that's the local computer ip.
         */
        public static void InitializeLocalConnection()
        {
            //local server
            var server = new WebSocketServer("ws://127.0.0.1:8181");

            //When a socket opens, add it to the client list, when it closes, remove it,
            //and when the socket recieves a message, transmit the message.
            server.Start(socket =>
            {
                socket.OnOpen = () =>
                {
                    _clients.Add(socket);
                };

                socket.OnClose = () =>
                {
                    _clients.Remove(socket);
                };

                socket.OnMessage = message =>
                {

                };
            });
        }

        /*
         * starting up the public websocket server
         * we use 127.0.0.1:8181 because that's the local computer ip.
         * this is run after the window loads so as to not slow down the window loading
         * */
        public void InitializePublicConnection()
        {
            try{
                //server on the public ip
                var server1 = new WebSocketServer("ws://" + GetPublicIP() + ":8181");

                //When a socket opens, add it to the client list, when it closes, remove it,
                //and when the socket recieves a message, transmit the message.
                server1.Start(socket =>
                {
                    socket.OnOpen = () =>
                    {
                        _clients.Add(socket);
                    };

                    socket.OnClose = () =>
                    {
                        _clients.Remove(socket);
                    };

                    socket.OnMessage = message =>
                    {

                    };
                });
                PublicConnectionStatusText = "Public Connection Started";

            }
            catch(System.Net.WebException e)
            {
                PublicConnectionStatusText = "Public Connection Failed";
            }
        }

        /*Sets up the kinect side of the server
         */
        public void InitializeKinect()
        {
            //Finding the sensor and setting it to a variable so it can be used,
            this.kinectSensor = KinectSensor.GetDefault();
            //initializing the coordinate mapper,
            this.coordinateMapper = this.kinectSensor.CoordinateMapper;
            //Setting up the bones and colors to be displayed in the skeleton
            SetUpBones();

            //Get the frame reader from the kinect.
            //We use multi-source because we are reading two difference kinds of sensors.
            this.multiSourceFrameReader =
                this.kinectSensor.OpenMultiSourceFrameReader(
                FrameSourceTypes.Color
                | FrameSourceTypes.Body);


            //Making the function "Reader_MultiSourceFrameArrived" run every time a frame comes in
            this.multiSourceFrameReader.MultiSourceFrameArrived +=
                this.Reader_MultiSourceFrameArrived;

            // set IsAvailableChanged event notifier
            this.kinectSensor.IsAvailableChanged +=
                this.Sensor_IsAvailableChanged;

            //Set the data context
            this.DataContext = this;

            //Open the sensor (begins reading)
            this.kinectSensor.Open();


        }

        /*Defining the bones to be displayed a Tuple containing two joint types,
         * and defining the colors to be displayed for each of the six possible bodies.
         */
        private void SetUpBones()
        {
            // a bone defined as a line between two joints
            this.bones = new List<Tuple<JointType, JointType>>();

            // Torso
            this.bones.Add(new Tuple<JointType, JointType>(JointType.Head, JointType.Neck));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.Neck, JointType.SpineShoulder));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.SpineShoulder, JointType.SpineMid));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.SpineMid, JointType.SpineBase));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.SpineShoulder, JointType.ShoulderRight));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.SpineShoulder, JointType.ShoulderLeft));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.SpineBase, JointType.HipRight));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.SpineBase, JointType.HipLeft));

            // Right Arm
            this.bones.Add(new Tuple<JointType, JointType>(JointType.ShoulderRight, JointType.ElbowRight));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.ElbowRight, JointType.WristRight));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.WristRight, JointType.HandRight));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.HandRight, JointType.HandTipRight));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.WristRight, JointType.ThumbRight));

            // Left Arm
            this.bones.Add(new Tuple<JointType, JointType>(JointType.ShoulderLeft, JointType.ElbowLeft));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.ElbowLeft, JointType.WristLeft));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.WristLeft, JointType.HandLeft));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.HandLeft, JointType.HandTipLeft));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.WristLeft, JointType.ThumbLeft));

            // Right Leg
            this.bones.Add(new Tuple<JointType, JointType>(JointType.HipRight, JointType.KneeRight));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.KneeRight, JointType.AnkleRight));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.AnkleRight, JointType.FootRight));

            // Left Leg
            this.bones.Add(new Tuple<JointType, JointType>(JointType.HipLeft, JointType.KneeLeft));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.KneeLeft, JointType.AnkleLeft));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.AnkleLeft, JointType.FootLeft));

            // populate body colors, one for each BodyIndex
            this.bodyColors = new List<Pen>();

            this.bodyColors.Add(new Pen(Brushes.Red, 6));
            this.bodyColors.Add(new Pen(Brushes.Orange, 6));
            this.bodyColors.Add(new Pen(Brushes.Green, 6));
            this.bodyColors.Add(new Pen(Brushes.Blue, 6));
            this.bodyColors.Add(new Pen(Brushes.Indigo, 6));
            this.bodyColors.Add(new Pen(Brushes.Violet, 6));
        }

        /*When a frame arrives from the kinect (which is 30 fps),
         * calls the functions that updates the color and skeleton display,
         * and the function that transmits the body data
         */
        private void Reader_MultiSourceFrameArrived(
            object sender,
            MultiSourceFrameArrivedEventArgs e)
        {
            MultiSourceFrame multiSourceFrame = e.FrameReference.AcquireFrame();

            //make sure the frame wasn't dropped
            if (multiSourceFrame == null)
            {
                return;
            }

            //Initialize frames to grab individual frames from the multisource frame.
            ColorFrame colorFrame = null;
            BodyFrame bodyFrame = null;

            //Not split because the raw data doesn't need much processing to be displayed
            using (colorFrame =
                multiSourceFrame.ColorFrameReference.AcquireFrame())
            {
                ShowColorFrame(colorFrame);
            }
            //Split into three functions to clearly show how things are working
            //and so the display and transmission don't interfere with each other
            using (bodyFrame =
                multiSourceFrame.BodyFrameReference.AcquireFrame())
            {
                GetBodyJoints(bodyFrame);
                if(dataReceived)
                {
                    using (DrawingContext dc = this.drawingGroup.Open())
                    {
                        ShowBodyJoints(dc);
                    }

                    TransmitBodyJoints();
                }

            }
        }

        //Reads in the colorFrame
        private void ShowColorFrame(ColorFrame colorFrame)
        {
            if (colorFrame != null)
            {
                FrameDescription colorFrameDescription =
                    colorFrame.FrameDescription;
                using (KinectBuffer colorBuffer = colorFrame.LockRawImageBuffer())
                {
                    this.colorBitmap.Lock();

                    // verify data and write the new color frame data to the display bitmap
                    if ((colorFrameDescription.Width == this.colorBitmap.PixelWidth) && (colorFrameDescription.Height == this.colorBitmap.PixelHeight))
                    {
                        colorFrame.CopyConvertedFrameDataToIntPtr(
                            this.colorBitmap.BackBuffer,
                            (uint)(colorFrameDescription.Width * colorFrameDescription.Height * 4),
                            ColorImageFormat.Bgra);

                        this.colorBitmap.AddDirtyRect(new Int32Rect(0, 0, this.colorBitmap.PixelWidth, this.colorBitmap.PixelHeight));
                    }

                    this.colorBitmap.Unlock();
                }
            }
        }
        
        // Reads in the bodyFrame (if it contains bodies)
        private void GetBodyJoints(BodyFrame bodyFrame)
        {
            //Makes sure the bodyFrame contains bodies, and if so, puts the data in bodies
            if(bodyFrame != null)
            {
                if (this.bodies == null)
                {
                    bodies = new Body[bodyFrame.BodyCount];
                }
                // The first time GetAndRefreshBodyData is called, Kinect will allocate each Body in the array.
                // As long as those body objects are not disposed and not set to null in the array,
                // those body objects will be re-used.
                bodyFrame.GetAndRefreshBodyData(this.bodies);
                dataReceived = true;
            }
        }
        /* Draws tracked bodies using random colors,
         * inside a window, clipping outside joints,
         * and showing bone thickness by confidence level.
         */
        private void ShowBodyJoints(DrawingContext dc)
        {
            dc.DrawRectangle(Brushes.Black, null, new Rect(0.0,0.0, this.displayWidth, this.displayHeight));

            int penIndex = 0;
            foreach (Body body in this.bodies)
            {
                Pen drawPen = this.bodyColors[penIndex++];

                if (body.IsTracked)
                {
                    this.DrawClippedEdges(body, dc);

                    IReadOnlyDictionary<JointType, Joint> joints = body.Joints;

                    // convert the joint points to depth (display) space
                    Dictionary<JointType, Point> jointPoints = new Dictionary<JointType, Point>();

                    foreach (JointType jointType in joints.Keys)
                    {
                        // sometimes the depth(Z) of an inferred joint may show as negative
                        // clamp down to 0.1f to prevent coordinatemapper from returning (-Infinity, -Infinity)
                        CameraSpacePoint position = joints[jointType].Position;
                        if (position.Z < 0)
                        {
                            position.Z = InferredZPositionClamp;
                        }

                        DepthSpacePoint depthSpacePoint = this.coordinateMapper.MapCameraPointToDepthSpace(position);
                        jointPoints[jointType] = new Point(depthSpacePoint.X, depthSpacePoint.Y);
                    }

                    this.DrawBody(joints, jointPoints, dc, drawPen);

                    //Extra drawing showing handstates
                    this.DrawHand(body.HandLeftState, jointPoints[JointType.HandLeft], dc);
                    this.DrawHand(body.HandRightState, jointPoints[JointType.HandRight], dc);


                }
            }
            // prevent drawing outside of our render area
            this.drawingGroup.ClipGeometry = new RectangleGeometry(new Rect(0.0, 0.0, this.displayWidth, this.displayHeight));
        }

        /// <summary>
        /// Draws a body
        /// </summary>
        /// <param name="joints">joints to draw</param>
        /// <param name="jointPoints">translated positions of joints to draw</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        /// <param name="drawingPen">specifies color to draw a specific body</param>
        private void DrawBody(IReadOnlyDictionary<JointType, Joint> joints, IDictionary<JointType, Point> jointPoints, DrawingContext drawingContext, Pen drawingPen)
        {
            // Draw the bones
            foreach (var bone in this.bones)
            {
                this.DrawBone(joints, jointPoints, bone.Item1, bone.Item2, drawingContext, drawingPen);
            }

            // Draw the joints
            foreach (JointType jointType in joints.Keys)
            {
                Brush drawBrush = null;

                TrackingState trackingState = joints[jointType].TrackingState;

                if (trackingState == TrackingState.Tracked)
                {
                    drawBrush = this.trackedJointBrush;
                }
                else if (trackingState == TrackingState.Inferred)
                {
                    drawBrush = this.inferredJointBrush;
                }

                if (drawBrush != null)
                {
                    drawingContext.DrawEllipse(drawBrush, null, jointPoints[jointType], JointThickness, JointThickness);
                }
            }
        }

        /// <summary>
        /// Draws one bone of a body (joint to joint)
        /// </summary>
        /// <param name="joints">joints to draw</param>
        /// <param name="jointPoints">translated positions of joints to draw</param>
        /// <param name="jointType0">first joint of bone to draw</param>
        /// <param name="jointType1">second joint of bone to draw</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        /// /// <param name="drawingPen">specifies color to draw a specific bone</param>
        private void DrawBone(IReadOnlyDictionary<JointType, Joint> joints, IDictionary<JointType, Point> jointPoints, JointType jointType0, JointType jointType1, DrawingContext drawingContext, Pen drawingPen)
        {
            Joint joint0 = joints[jointType0];
            Joint joint1 = joints[jointType1];

            // If we can't find either of these joints, exit
            if (joint0.TrackingState == TrackingState.NotTracked ||
                joint1.TrackingState == TrackingState.NotTracked)
            {
                return;
            }

            // We assume all drawn bones are inferred unless BOTH joints are tracked
            Pen drawPen = this.inferredBonePen;
            if ((joint0.TrackingState == TrackingState.Tracked) && (joint1.TrackingState == TrackingState.Tracked))
            {
                drawPen = drawingPen;
            }

            drawingContext.DrawLine(drawPen, jointPoints[jointType0], jointPoints[jointType1]);
        }

        /// <summary>
        /// Draws a hand symbol if the hand is tracked: red circle = closed, green circle = opened; blue circle = lasso
        /// </summary>
        /// <param name="handState">state of the hand</param>
        /// <param name="handPosition">position of the hand</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        private void DrawHand(HandState handState, Point handPosition, DrawingContext drawingContext)
        {
            switch (handState)
            {
                case HandState.Closed:
                    drawingContext.DrawEllipse(this.handClosedBrush, null, handPosition, HandSize, HandSize);
                    break;

                case HandState.Open:
                    drawingContext.DrawEllipse(this.handOpenBrush, null, handPosition, HandSize, HandSize);
                    break;

                case HandState.Lasso:
                    drawingContext.DrawEllipse(this.handLassoBrush, null, handPosition, HandSize, HandSize);
                    break;
            }
        }

        /// <summary>
        /// Draws indicators to show which edges are clipping body data
        /// </summary>
        /// <param name="body">body to draw clipping information for</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        private void DrawClippedEdges(Body body, DrawingContext drawingContext)
        {
            FrameEdges clippedEdges = body.ClippedEdges;

            if (clippedEdges.HasFlag(FrameEdges.Bottom))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, this.displayHeight - ClipBoundsThickness, this.displayWidth, ClipBoundsThickness));
            }

            if (clippedEdges.HasFlag(FrameEdges.Top))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, 0, this.displayWidth, ClipBoundsThickness));
            }

            if (clippedEdges.HasFlag(FrameEdges.Left))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, 0, ClipBoundsThickness, this.displayHeight));
            }

            if (clippedEdges.HasFlag(FrameEdges.Right))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(this.displayWidth - ClipBoundsThickness, 0, ClipBoundsThickness, this.displayHeight));
            }
        }

        private void TransmitBodyJoints()
        {

            /* This section counts the number of tracked bodies,
             * creates a new body array of that size,
             * and inserts only the tracked bodies into that array
             * The result is going from bodies, which may have null and untracked bodies,
             * to validBodies, which only contains tracked bodies, and without extra space in the array.
             */
            int i = 0;
            int j = 0;
            foreach (Body body in bodies)
            {
                if (body.IsTracked)
                {
                    i++;
                }
            }
            validBodies = new Body[i];
            i = 0;
            foreach (Body body in bodies)
            {
                if (body.IsTracked)
                {
                    validBodies[i] = bodies[j];
                    i++;
                }
                j++;
            }

            //Turning the tracked bodies into json
            string json = validBodies.Serialize();

            //Copying the list so the foreach loop doesn't deal with a moving target.
            List<IWebSocketConnection> _clientsTransmit = new List<IWebSocketConnection>(_clients);

            //Transmitting the json
            foreach (var socket in _clientsTransmit)
            {
                socket.Send(json);
            }
        }

        //Set up the display showing the color
        private void SetupColorDisplay()
        {
            FrameDescription colorFrameDescription = this.kinectSensor.ColorFrameSource.CreateFrameDescription(ColorImageFormat.Bgra);
            this.colorBitmap = new WriteableBitmap(colorFrameDescription.Width,
                colorFrameDescription.Height, 96.0, 96.0, PixelFormats.Bgr32, null);
        }


        //Set up the display showing the body joints
        private void SetupBodyJointsDisplay()
        {
            // get the depth (display) extents
            FrameDescription frameDescription = this.kinectSensor.DepthFrameSource.FrameDescription;

            // get size of joint space
            this.displayWidth = frameDescription.Width;
            this.displayHeight = frameDescription.Height;

            // Create the drawing group we'll use for drawing
            this.drawingGroup = new DrawingGroup();

            // Create an image source that we can use in our image control
            this.imageSource2 = new DrawingImage(this.drawingGroup);
        }

        //Copy pasted from stackoverflow (see sources)
        //Gets the public IP for the current computer
        public static string GetPublicIP()
        {
                string url = "http://checkip.dyndns.org";
                System.Net.WebRequest req = System.Net.WebRequest.Create(url);
                System.Net.WebResponse resp = req.GetResponse();
                System.IO.StreamReader sr = new System.IO.StreamReader(resp.GetResponseStream());
                string response = sr.ReadToEnd().Trim();
                string[] a = response.Split(':');
                string a2 = a[1].Substring(1);
                string[] a3 = a2.Split('<');
                string a4 = a3[0];
                return a4;


        }

        /// <summary>
        /// Execute start up tasks.
        /// InitializePublicConnection is here instead of in Main Window,
        /// because it is sometimes slow.
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void MainWindow_Activated(object sender, EventArgs e)
        {
            if (!publicStarted)
            {
                InitializePublicConnection();
                publicStarted = true;
            }
        }

        /// <summary>
        /// Execute shutdown tasks
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            if (this.multiSourceFrameReader != null)
            {
                this.multiSourceFrameReader.Dispose();
                this.multiSourceFrameReader = null;
            }

            if (this.kinectSensor != null)
            {
                this.kinectSensor.Close();
                this.kinectSensor = null;
            }
        }

        /* Changes the status text based on whether the kinect is connected
         */
        private void Sensor_IsAvailableChanged(object sender,
            IsAvailableChangedEventArgs args)
        {
            this.KinectStatusText = this.kinectSensor.IsAvailable ?
                "Kinect Running" : "Kinect Not Available";
        }
    }
}

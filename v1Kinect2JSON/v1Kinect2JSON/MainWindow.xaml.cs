/*
 * Authors: Isaac Zylstra and Victor Norman @ Calvin College, Grand Rapids, MI.
 * Contact: vtn2@calvin.edu
 */

using Fleck;
using Microsoft.Kinect;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace v1Kinect2JSON
{

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        //The list of websockets that this server transmits to
        static List<IWebSocketConnection> _clients = new List<IWebSocketConnection>();

        //The kinect sensor, and the mapper (to map the skeleton to the display window)
        private KinectSensor kinectSensor = null;
        private CoordinateMapper coordinateMapper = null;

        //The list of bones and colors (for the skeleton display)
        private List<Tuple<JointType, JointType>> bones;
        private List<Pen> bodyColors;

        //the width and height of the joint space
        private int displayWidth = 640;
        private int displayHeight = 480;

        //The body array that is read from the bodyFrame (may contain null or untracked bodies)
        private Skeleton[] bodies = null;
        //The body array that only contains tracked bodies
        private Skeleton[] validBodies = null;
        // a boolean checking if the current bodyFrame contains bodies
        bool dataReceived = false;

        //The color bitmap to be displayed
        private WriteableBitmap colorBitmap = null;

        //Intermediate storage for the color data received from the camera
        private byte[] colorPixels;

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
        private string kinectStatusText = "Kinect not ready. Please connect the Kinect and relaunch.";
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
            if(this.kinectSensor != null)
            {
                SetupColorDisplay();
                SetupBodyJointsDisplay();
                InitializeLocalConnection();
            }


            InitializeComponent();
        }
        /*
         * starting up the local websocket server
         * we use 127.0.0.1:8181 because that's the local computer ip.
         */
        public void InitializeLocalConnection()
        {
            //local server
            var server1 = new WebSocketServer("ws://127.0.0.1:8181");
            ServerSetup(ref server1);
        }

        /*
         * starting up the public websocket server
         * we use 127.0.0.1:8181 because that's the local computer ip.
         * this is run after the window loads so as to not slow down the window loading
         * */
        public void InitializePublicConnection()
        {
            try
            {
                //server on the public ip
                var server2 = new WebSocketServer("ws://" + GetPublicIP() + ":8181");
                ServerSetup(ref server2);
                PublicConnectionStatusText = "Public Connection Started at " + GetPublicIP();
            }
            catch (System.Net.WebException e)
            {
                PublicConnectionStatusText = "Public Connection Failed";
            }
        }

        /* Setting up the servers (they all have the same properties other than IP, which is in the declaration)
         * When a socket opens, add it to the client list, when it closes, remove it,
         * and when the socket recieves a message, transmit the message.
         */
        public void ServerSetup(ref WebSocketServer server)
        {
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

        /*Sets up the kinect side of the server
         */
        public void InitializeKinect()
        {
            // Look through all sensors and start the first connected one.
            // This requires that a Kinect is connected at the time of app startup.
            // To make your app robust against plug/unplug, 
            // it is recommended to use KinectSensorChooser provided in Microsoft.Kinect.Toolkit (See components in Toolkit Browser).
            foreach (var potentialSensor in KinectSensor.KinectSensors)
            {
                if (potentialSensor.Status == KinectStatus.Connected)
                {
                    this.kinectSensor = potentialSensor;
                    break;
                }
            }

            if (kinectSensor != null)
            {
                //enabling the various streams
                kinectSensor.ColorStream.Enable();
                kinectSensor.SkeletonStream.Enable();

                //initializing the coordinate mapper,
                this.coordinateMapper = this.kinectSensor.CoordinateMapper;

                // Add an event handler to be called whenever there is new color frame data
                this.kinectSensor.ColorFrameReady += this.SensorColorFrameReady;

                // Add an event handler to be called whenever there is new skeleton frame data
                this.kinectSensor.SkeletonFrameReady += this.SensorSkeletonFrameReady;

                // Start the sensor!
                try
                {
                    KinectStatusText = "Connected";
                    this.kinectSensor.Start();
                }
                catch (Exception e)
                {
                    this.kinectSensor = null;
                }
            }

            //Setting up the bones and colors to be displayed in the skeleton
            SetUpBones();

            //Set the data context
            this.DataContext = this;
        }

        /*Defining the bones to be displayed a Tuple containing two joint types,
         * and defining the colors to be displayed for each of the six possible bodies.
         */
        private void SetUpBones()
        {
            // a bone defined as a line between two joints
            this.bones = new List<Tuple<JointType, JointType>>();

            // Torso
            this.bones.Add(new Tuple<JointType, JointType>(JointType.Head, JointType.ShoulderCenter));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.ShoulderCenter, JointType.Spine));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.Spine, JointType.HipCenter));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.ShoulderCenter, JointType.ShoulderRight));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.ShoulderCenter, JointType.ShoulderLeft));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.HipCenter, JointType.HipRight));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.HipCenter, JointType.HipLeft));

            // Right Arm
            this.bones.Add(new Tuple<JointType, JointType>(JointType.ShoulderRight, JointType.ElbowRight));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.ElbowRight, JointType.WristRight));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.WristRight, JointType.HandRight));

            // Left Arm
            this.bones.Add(new Tuple<JointType, JointType>(JointType.ShoulderLeft, JointType.ElbowLeft));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.ElbowLeft, JointType.WristLeft));
            this.bones.Add(new Tuple<JointType, JointType>(JointType.WristLeft, JointType.HandLeft));

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
        private void SensorColorFrameReady(
            object sender,
            ColorImageFrameReadyEventArgs e)
        {
            using (ColorImageFrame colorFrame = e.OpenColorImageFrame())
            {
                ShowColorFrame(colorFrame);
            }
        }

        private void SensorSkeletonFrameReady(
           object sender,
           SkeletonFrameReadyEventArgs e)
        {
            using (SkeletonFrame skeletonFrame = e.OpenSkeletonFrame())
            {
                if (skeletonFrame != null)
                {
                    bodies = new Skeleton[skeletonFrame.SkeletonArrayLength];
                    skeletonFrame.CopySkeletonDataTo(bodies);
                    dataReceived = true;
                }
            }
            
            if (dataReceived)
            {
                using (DrawingContext dc = this.drawingGroup.Open())
                {
                    ShowBodyJoints(dc);
                }
                TransmitBodyJoints();
            }
        }

        //Reads in the colorFrame
        private void ShowColorFrame(ColorImageFrame colorFrame)
        {
            if (colorFrame != null)
            {
                // Copy the pixel data from the image to a temporary array
                colorFrame.CopyPixelDataTo(this.colorPixels);

                // Write the pixel data into our bitmap
                this.colorBitmap.WritePixels(
                    new Int32Rect(0, 0, this.colorBitmap.PixelWidth, this.colorBitmap.PixelHeight),
                    this.colorPixels,
                    this.colorBitmap.PixelWidth * sizeof(int),
                    0);
            }
        }

        /* Draws tracked bodies using random colors,
         * inside a window, clipping outside joints,
         * and showing bone thickness by confidence level.
         */
        private void ShowBodyJoints(DrawingContext dc)
        {
            dc.DrawRectangle(Brushes.Black, null, new Rect(0.0, 0.0, this.displayWidth, this.displayHeight));

            int penIndex = 0;
            foreach (Skeleton body in this.bodies)
            {
                Pen drawPen = this.bodyColors[penIndex++];

                if (body.TrackingState == SkeletonTrackingState.Tracked)
                {
                    this.DrawClippedEdges(body, dc);
                    JointCollection joints = body.Joints;
                    this.DrawBody(joints, dc, drawPen);
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
        private void DrawBody(JointCollection joints, DrawingContext drawingContext, Pen drawingPen)
        {
            // Draw the bones
            foreach (var bone in this.bones)
            {
                this.DrawBone(joints, bone.Item1, bone.Item2, drawingContext, drawingPen);
            }

            // Draw the joints
            foreach (Joint joint in joints)
            {
                Brush drawBrush = null;

                JointTrackingState trackingState = joint.TrackingState;

                if (trackingState == JointTrackingState.Tracked)
                {
                    drawBrush = this.trackedJointBrush;
                }
                else if (trackingState == JointTrackingState.Inferred)
                {
                    drawBrush = this.inferredJointBrush;
                }

                if (drawBrush != null)
                {
                    drawingContext.DrawEllipse(drawBrush, null, this.SkeletonPointToScreen(joint.Position), JointThickness, JointThickness);
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
        private void DrawBone(JointCollection joints, JointType jointType0, JointType jointType1, DrawingContext drawingContext, Pen drawingPen)
        {
            Joint joint0 = joints[jointType0];
            Joint joint1 = joints[jointType1];

            // If we can't find either of these joints, exit
            if (joint0.TrackingState == JointTrackingState.NotTracked ||
                joint1.TrackingState == JointTrackingState.NotTracked)
            {
                return;
            }

            // We assume all drawn bones are inferred unless BOTH joints are tracked
            Pen drawPen = this.inferredBonePen;
            if ((joint0.TrackingState == JointTrackingState.Tracked) && (joint1.TrackingState == JointTrackingState.Tracked))
            {
                drawPen = drawingPen;
            }

            drawingContext.DrawLine(drawPen, this.SkeletonPointToScreen(joint0.Position), this.SkeletonPointToScreen(joint1.Position));
        }

        /// <summary>
        /// Maps a SkeletonPoint to lie within our render space and converts to Point
        /// </summary>
        /// <param name="skelpoint">point to map</param>
        /// <returns>mapped point</returns>
        private Point SkeletonPointToScreen(SkeletonPoint skelpoint)
        {
            // Convert point to depth space.  
            // We are not using depth directly, but we do want the points in our 640x480 output resolution.
            DepthImagePoint depthPoint = this.kinectSensor.CoordinateMapper.MapSkeletonPointToDepthPoint(skelpoint, DepthImageFormat.Resolution640x480Fps30);
            return new Point(depthPoint.X, depthPoint.Y);
        }

        /// <summary>
        /// Draws indicators to show which edges are clipping body data
        /// </summary>
        /// <param name="body">body to draw clipping information for</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        private void DrawClippedEdges(Skeleton body, DrawingContext drawingContext)
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
            foreach (Skeleton body in bodies)
            {
                if (body.TrackingState == SkeletonTrackingState.Tracked)
                {
                    i++;
                }
            }
            validBodies = new Skeleton[i];
            i = 0;
            foreach (Skeleton body in bodies)
            {
                if (body.TrackingState == SkeletonTrackingState.Tracked)
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
            this.colorBitmap = new WriteableBitmap(this.kinectSensor.ColorStream.FrameWidth,
                this.kinectSensor.ColorStream.FrameHeight, 96.0, 96.0, PixelFormats.Bgr32, null);

            // Allocate space to put the pixels we'll receive
            this.colorPixels = new byte[this.kinectSensor.ColorStream.FramePixelDataLength];
        }


        //Set up the display showing the body joints
        private void SetupBodyJointsDisplay()
        {
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
        /// Execute startup tasks
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void WindowLoaded(object sender, RoutedEventArgs e)
        {

        }

        /// <summary>
        /// InitializePublicConnection is here instead of in Main Window,
        /// because it is sometimes slow.
        /// Due to this, blank window pops up while public connection is starting.
        /// publicStarted makes sure that the connection is only started once.
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
            if (null != this.kinectSensor)
            {
                this.kinectSensor.Stop();
            }
        }
    }
}

﻿//this is a comment. don't ask me why it's here.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
using Microsoft.Kinect.Toolkit;

namespace BucketGame
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

        ImageObject imageObject;

        bool currentlyPlaying = false;
        JointSelection jointSelectionWindow;
        JointType currentlyUsedJoint = JointType.HandRight;
        KinectSensor sensor;
        Status statusWindow;
        Skeleton skeleton;
        static Random random = new Random();
        bool HasTouchedObject = false;
        ImageObject[] Targets = new ImageObject[Consts.BagPaths.Length];
        ImageObject CurrentTarget;

        //this variable is used by the 
        bool firstTime = true;
        public MainWindow()
        {
            jointSelectionWindow = new JointSelection(this);
            jointSelectionWindow.Show();

            InitializeComponent();
            statusWindow = new Status();
            statusWindow.Show();

            //KinectSensor.KinectSensors is an array with sensor objects, with null where there is none.
            //from this array, choose the first that is not null. if there is none, choose null.
            sensor = KinectSensor.KinectSensors.FirstOrDefault(s => s != null);
            
            //if this occurs, then FirstOrDefault returned null - therefore, there was no kinect object
            //that wasn't null in the KinectSensor.KinectSensors array. This probably means
            //there is no kinect sensor connected to the computer (and to power)
            if (sensor == default(KinectSensor))
            {
                MessageBox.Show("Kinect sensor is null");
                return;
            }

            //the following loop initializes and locates the targets on the screen.
            int x = 25 , y = Consts.FrameHeight - Consts.PortalSize * 2;
            for (int i = 0; i < Consts.ImageObjectPaths.Length; i++) //iterate over Consts.ImageObejctPaths,
                                                                //wchich is an array of the local paths of the
                                                                //images of the targets.
            {
                //iteratively initialize the Targets array
                Targets[i] = new ImageObject(Consts.BagPaths[i], Consts.PortalSize * 2, Consts.PortalSize * 2);
                
                //this is just a reference for conviniece. Everytime in this loop when there is an "it"
                // (short for iterated), it is equivalent to writing Targets[i]
                ImageObject it = Targets[i];

                //just alignment stuff
                it.HorizontalAlignment = HorizontalAlignment.Left;
                it.VerticalAlignment = VerticalAlignment.Top;
               
                //Add this to the container
                targetsCanvas.Children.Add(it);

                //Locate on the screen
                Canvas.SetLeft(it,x);
                Canvas.SetTop(it, y);

                //the variable x changes so each target will be located just in the right place.
                x += Consts.DistanceBetweenPortals;
                
            }//Yay, we were done with initializing the targets!! that wasn't so hard, now, wasn't it?



            //let the CurrentTarget be the first target, though, this line isn't very significant
            CurrentTarget = Targets[0];
            
            //ENABLE!
            sensor.ColorStream.Enable();
            sensor.DepthStream.Enable();
            sensor.SkeletonStream.Enable();

            //everytime we get info from the sensor (30 times a sec), call our function, sensor_AllFramesReady
            sensor.AllFramesReady += sensor_AllFramesReady; //to go this method, put cursor on it, then F12
            
            //action!
            sensor.Start();
        }



        public static void MoveTo(UIElement elem, Point point) //moves the given UIElement to the given point
        {
            Canvas.SetLeft(elem, point.X);
            Canvas.SetTop(elem, point.Y);   
        }
        

        //this method is called whenever we get all frames from the sensor (color, depth and skeleton frame) - 
        //30 times a sec, that is. The sender object will be the kinect sensor, and the e object
        //consists information about the event that just happened - in this case, it will have
        //info from the sensor (the frame we got, the skeletons, etc.)
        void sensor_AllFramesReady(object sender, AllFramesReadyEventArgs e)
        {
            //if we are currently playing, update the label in the status window that reads the current
            //time of the stopwatch of the game
            if (currentlyPlaying)
            {
                statusWindow.UpdateCurrentTime();
            }

            //in this block, open the DepthImageFrame from the object e, that consist
            using (DepthImageFrame depthImageFrame = e.OpenDepthImageFrame())
            {
                if (depthImageFrame == null) //probably just random lags that should be ignored
                {
                    return; 
                }

                using (ColorImageFrame colorFrame = e.OpenColorImageFrame())
                {
                    if (colorFrame != null)
                    {
                        Util.DrawOnImage(colorFrame, frame); //draw the image from the sensor on our Image object
                    }
                }

                if (!currentlyPlaying) { return; } //if we aren't currently playing, then stop this method here.

                using (SkeletonFrame skeletonFrame = e.OpenSkeletonFrame())
                {
                    if (skeletonFrame != null)
                    {
                        /*
                        if (!sensor.IsRunning)
                        {
                            StatusLabel.Content = "Kinect isn't running (check is plugged in)";
                            return;
                        }
                         */

                        /* The skeletonFrame object only lets us recieve info about the skeletons in a.. weird way.
                         * It says - give me an empty array of skeletons,  with proper length, and I will
                         * copy the skeleton info into it.
                         * Oh, Microsoft....
                         */
                        Skeleton[] skeletons = new Skeleton[skeletonFrame.SkeletonArrayLength];
                        skeletonFrame.CopySkeletonDataTo(skeletons);
                        
                        //this weird line just reads:
                        //taken our array, called "skeletons", and from which choose the first that is not-null
                        //and is tracked. If there is no such skeleton, choose null.
                        skeleton = skeletons.FirstOrDefault(s => s != null && s.TrackingState == SkeletonTrackingState.Tracked);
                        
                        if (skeleton == default(Skeleton)) //if the FirstOrDefault returned null,
                                                          //which happens when the array has only null or
                                                         //non-tracked skeletons.
                        {
                            StatusLabel.Content = "No skeleton tracked";
                            return;
                        }
                        else
                        {
                            StatusLabel.Content = "";
                        }
                       
                        
                        Point locationOfCurrentJoint = Util.GetPoint(skeleton, currentlyUsedJoint, depthImageFrame);

                        //distance of the player's joint from the target
                        double distance = Util.Distance(locationOfCurrentJoint, new Point(Canvas.GetLeft(imageObject),Canvas.GetTop(imageObject)));

                        if (HasTouchedObject) //if the player already touched the object...
                        {
                            //move the object to the player's joint
                            MoveTo(imageObject, locationOfCurrentJoint);
                            
                            //this will be the location of the target
                            Point targetLocation = new Point(Canvas.GetLeft(CurrentTarget) + Consts.PortalSize, Canvas.GetTop(CurrentTarget) + Consts.PortalSize);
                            
                            //this will be the player's joint's distance from the target
                            double distanceFromTarget = Util.Distance(targetLocation, Util.ToPoint(locationOfCurrentJoint));
                            
                            //if we basically touched the target - then...
                            if (distanceFromTarget <= Consts.TouchingDistance)
                            {
                                HasTouchedObject = false;
                                CreateNextImage(locationOfCurrentJoint);
                            }
                            
                        }

                        //if we haven't touched the target, but now we just firsly did, then..
                        else if (distance <= Consts.ShapeRadius)
                        {
                            HasTouchedObject = true;
                        }
                        
                    }
                }
            }
        }


        //this function is called when the game first starts.
        public void GameStart()
        {
            //this variable is so that we know whether not to add points in the CreateNext method.
            firstTime = true;

            //update the status window
            statusWindow.TimeStarted = DateTime.Now.Ticks;
            statusWindow.Score = 0;
    

            CreateNextImage(new Point(0, 0));

        }

        //this method safely changes the current joint randomly, and updates the status window
        public void ChangeJoint()
        {
            try
            {
                currentlyUsedJoint = jointSelectionWindow.Selected.ChooseRandom(random);
            }
            catch (Exception e)
            {
                currentlyUsedJoint = Consts.DefaultJoints[0];
            }
            statusWindow.Joint = currentlyUsedJoint;
        }

        //this method creates the next shape and updates everything when the game starts
        //and whenever the player scores. BTW, we know to differ these situations with the firstTime variable.
        public void CreateNextImage(Point rightHand)
        {
            //current willl be the index of the next target.
            int current = random.Next(0, Consts.ImageObjectPaths.Length);

            //choose a random point at the top half of the screen
            //(the top half - because we don't want it to be too close to the targets)
            Point p = Util.RandomPointAtTopHalfOfScreen(random);

            //omer, I don't get it. please comment this for us.
            if (imageObject == null)
            {
                imageObject = new ImageObject(Consts.ImageObjectPaths[current], 2 * Consts.ShapeRadius, 2 * Consts.ShapeRadius);
                targetsCanvas.Children.Add(imageObject);
            }
            else
            {
                imageObject.SetRelativeSource(Consts.ImageObjectPaths[current]);
            }

            //set the currentTarget
            CurrentTarget = Targets[current]; //current-target-target-current. does this count as a palindrome?

            //Move the imageObejct to this point
            MoveTo(imageObject, p);

            //we haven't touched THIS new object yet...
            HasTouchedObject = false;
            
            //change the joint used, randomly, then update the status window.
            ChangeJoint();

            //if this function was called because the player scored (and not because the game just started),
            //then add some to the current score.
            if (!firstTime)
            {
                statusWindow.Score += Consts.ScorePerShape;
            }

            //from now on and until the next game starts, we only call this method when the player scroed.
            firstTime = false;

            //if we basically just won...
            if (statusWindow.Score >= Consts.WantedScore)
            {
                currentlyPlaying = false; //we're done playing
                GameButton.Content = "Play"; //so change the button's content
                MessageBox.Show("You scored " + statusWindow.Score + "points in " + statusWindow.TimeString);
                //and give the  player a thumbs up
            }
        }

        
        
       

        private void Window_Closed(object sender, EventArgs e)
        {
            sensor.Stop();
            jointSelectionWindow.Close();
            statusWindow.Close();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            currentlyPlaying = !currentlyPlaying;
            GameButton.Content = currentlyPlaying ? "Stop" : "Start";
            if (currentlyPlaying)
            {
                GameStart();
            }
        }





        

      
    }
}
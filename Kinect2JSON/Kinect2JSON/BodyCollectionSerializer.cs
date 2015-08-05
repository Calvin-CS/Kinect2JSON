/*
 * Authors: Isaac Zylstra and Victor Norman @ Calvin College, Grand Rapids, MI.
 * Contact: vtn2@calvin.edu
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Kinect;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace Kinect2JSON
{
    /// <summary>
    /// Serializes a Kinect skeleton to JSON fromat.
    /// </summary>
    public static class BodyCollectionSerializer
    {
        //a body collection has a list of bodies.
        [DataContract]
        class JSONBodyCollection
        {
            [DataMember(Name = "bodies")]
            public List<JSONBody> Bodies { get; set; }
        }

        //a body has an ID, a list of joints, a left handstate, and a right handstate.
        [DataContract]
        class JSONBody
        {
            [DataMember(Name = "id")]
            public string ID { get; set; }

            [DataMember(Name = "joints")]
            public List<JSONJoint> Joints { get; set; }

            [DataMember(Name = "lhandstate")]
            public int LState { get; set; }

            [DataMember(Name = "rhandstate")]
            public int RState { get; set; }
        }

        //a joint has name and x, y, z coordinates that (should) correspond to the joints x, y, z coordinates. 
        [DataContract]
        class JSONJoint
        {
            [DataMember(Name = "name")]
            public string Name { get; set; }

            [DataMember(Name = "x")]
            public float X { get; set; }

            [DataMember(Name = "y")]
            public float Y { get; set; }

            [DataMember(Name = "z")]
            public float Z { get; set; }
        }

        /// <summary>
        /// Serializes an array of Kinect bodies into an array of JSON skeletons.
        /// </summary>
        /// <param name="bodies">The Kinect bodies.</param>
        /// <returns>A JSON representation of the bodies.</returns>
        public static string Serialize(this Body[] bodies)
        {
            JSONBodyCollection jsonBodies = new JSONBodyCollection { Bodies = new List<JSONBody>() };

            //Serializes all bodies in the body collection, regardless of content
            foreach (Body body in bodies)
            {
                JSONBody jsonBody = new JSONBody
                {
                    ID = body.TrackingId.ToString(),
                    Joints = new List<JSONJoint>(),
                    LState = (int)body.HandLeftState,
                    RState = (int)body.HandRightState
                };

                //Add all joints, again, regardless of content
                foreach (KeyValuePair<JointType, Joint> joint in body.Joints)
                {
                    jsonBody.Joints.Add(new JSONJoint
                    {
                        Name = joint.Key.ToString().ToLower(),
                        X = joint.Value.Position.X,
                        Y = joint.Value.Position.Y,
                        Z = joint.Value.Position.Z
                    });
                }
                //Bodies is the name of the body array contained in jsonBodies.
                //Here the jsonBody is added to the array.
                jsonBodies.Bodies.Add(jsonBody);
            }
            
            return Serialize(jsonBodies);
        }

        /// <summary>
        /// Serializes an object to JSON.
        /// I don't really know the details. Implementation is library (.NET) specific
        /// </summary>
        /// <param name="obj">The specified object.</param>
        /// <returns>A JSON representation of the object.</returns>
        private static string Serialize(object obj)
        {
            DataContractJsonSerializer serializer = new DataContractJsonSerializer(obj.GetType());

            using (MemoryStream ms = new MemoryStream())
            {
                serializer.WriteObject(ms, obj);
                byte[] temp = ms.ToArray();
                return Encoding.UTF8.GetString(temp,0,temp.Length);
            }
        }
    }
}

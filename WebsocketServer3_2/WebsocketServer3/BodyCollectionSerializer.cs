using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Kinect;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace WebsocketServer3_2
{
    /// <summary>
    /// Serializes a Kinect skeleton to JSON fromat.
    /// </summary>
    public static class BodyCollectionSerializer
    {
        [DataContract]
        class JSONBodyCollection
        {
            [DataMember(Name = "bodies")]
            public List<JSONBody> Bodies { get; set; }
        }

        [DataContract]
        class JSONBody
        {
            [DataMember(Name = "id")]
            public string ID { get; set; }

            [DataMember(Name = "joints")]
            public List<JSONJoint> Joints { get; set; }
        }

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
        /// <param name="skeletons">The Kinect bodies.</param>
        /// <param name="mapper">The coordinate mapper. (unused)</param>
        /// <param name="mode">Mode (color or depth). (unused)</param>
        /// <returns>A JSON representation of the bodies.</returns>
        public static string Serialize(this Body[] bodies)
        {
            JSONBodyCollection jsonBodies = new JSONBodyCollection { Bodies = new List<JSONBody>() };

            foreach (Body body in bodies)
            {
                JSONBody jsonBody = new JSONBody
                {
                    ID = body.TrackingId.ToString(),
                    Joints = new List<JSONJoint>()
                };

                foreach (KeyValuePair<JointType, Joint> joint in body.Joints)
                {
                    /*Point point = new Point();

                    switch (mode)
                    {
                        case Mode.Color:
                            ColorImagePoint colorPoint = mapper.MapSkeletonPointToColorPoint(joint.Position, ColorImageFormat.RgbResolution640x480Fps30);
                            point.X = colorPoint.X;
                            point.Y = colorPoint.Y;
                            break;
                        case Mode.Depth:
                            DepthImagePoint depthPoint = mapper.MapSkeletonPointToDepthPoint(joint.Position, DepthImageFormat.Resolution640x480Fps30);
                            point.X = depthPoint.X;
                            point.Y = depthPoint.Y;
                            break;
                        default:
                            break;
                    }*/

                    jsonBody.Joints.Add(new JSONJoint
                    {
                        Name = joint.Key.ToString().ToLower(),
                        X = joint.Value.Position.X,
                        Y = joint.Value.Position.Y,
                        Z = joint.Value.Position.Z
                    });
                }

                jsonBodies.Bodies.Add(jsonBody);
            }
            
            return Serialize(jsonBodies);
            //return "hello!";
        }

        /// <summary>
        /// Serializes an object to JSON.
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

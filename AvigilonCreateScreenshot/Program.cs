using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Xml;
using System.Xml.Serialization;
using AvigilonDotNet;

namespace AvigilonCreateScreenshot
{
    [Serializable]
    public class Camera
    {
        [System.Xml.Serialization.XmlElement("serverIp")]
        public string serverIp { get; set; }

        [System.Xml.Serialization.XmlElement("cameraId")]
        public uint cameraId { get; set; }

        [System.Xml.Serialization.XmlElement("id")]
        public string id { get; set; }

        [System.Xml.Serialization.XmlElement("direction")]
        public string direction { get; set; }
    }

    [Serializable]
    [XmlRoot("cameraCollection")]
    public class CameraCollection
    {
        [XmlArray("cameras")]
        [XmlArrayItem("camera", typeof(Camera))]
        public Camera[] Car { get; set; }
    }

    class Program
    {
        private static AvigilonSdk m_sdk;
        private static IAvigilonControlCenter m_controlCenter;
        private static IPEndPoint m_endPoint;
        private static IPAddress m_address;
        private static string m_userName = "";
        private static string m_password = "";
        private static CameraCollection cameras;

        private static void InitAvigilon()
        {
            // Create and initialize the control center SDK by passing the Avigilon
            // Control Center .NET SDK version the client application is expected to
            // run against.
            SdkInitParams initParams = new SdkInitParams(6, 2)
            {
                // Set to true to auto discover other Avigilon control center servers on the
                // network. If set to false, servers will have to be manually added via
                // AddNvr(IPEndPoint).
                AutoDiscoverNvrs = false,
                ServiceMode = true
            };

            // Create an instance of the AvigilonSdk class and call CreateInstance to
            // ensure application is compatible with SDK and no SDK components are missing.
            m_sdk = new AvigilonSdk();
            m_controlCenter = m_sdk.CreateInstance(initParams);
        }

        private static bool ParseCommandLine()
        {
            string[] commandLineArgs = Environment.GetCommandLineArgs();
            foreach (string arg in commandLineArgs)
            {
                if (arg.Length >= 2 && arg[0] == '-')
                {
                    string value = arg.Substring(2);
                    if (value.Length > 0)
                    {
                        if (arg[1] == 's')
                        {
                            if (IPAddress.TryParse(value, out IPAddress address))
                            {
                                m_address = address;
                            }
                        }
                        else if (arg[1] == 'u')
                        {
                            m_userName = value;
                        }
                        else if (arg[1] == 'p')
                        {
                            m_password = value;
                        }
                    }
                }
            }
            if (m_address == null)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Создание xml файла
        /// </summary>
        /// <param name="byteFrame">картина с камеры в виде массива байтов</param>
        /// <param name="logicalId">идентификатор камеры на сервере Avigilon</param>
        /// <param name="timeStamp">время создания кадра</param>
        private static void CreateXmlFile(byte[] byteFrame, uint logicalId, DateTime timeStamp)
        {
            GetIdAndDirection(logicalId, out string id, out string direction);

            if (id != "" && direction != "")
            {
                string folderPath = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName) + "/video";
                if (!File.Exists(folderPath))
                {
                    Directory.CreateDirectory(folderPath);
                }

                Random random = new Random();
                string fileName = $"{id}_{timeStamp:yyyyMMddHHmmss}_{random.Next(0, 9)}{random.Next(0, 9)}{random.Next(0, 9)}{random.Next(0, 9)}{random.Next(0, 9)}.xml";
                string filePath = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName) + "/video/" + fileName;

                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }

                //string fileNameJpg = $"{id}_{timeStamp:yyyyMMddHHmmss}_{random.Next(0, 9)}{random.Next(0, 9)}{random.Next(0, 9)}{random.Next(0, 9)}{random.Next(0, 9)}.jpg";
                //string filePathJpg = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName) + "/video/" + fileNameJpg;

                //if (File.Exists(filePathJpg))
                //{
                //    File.Delete(filePathJpg);
                //}

                //using (FileStream imageFile = new FileStream(filePathJpg, FileMode.Create))
                //{
                //    imageFile.Write(byteFrame, 0, byteFrame.Length);
                //    imageFile.Flush();
                //}

                XmlDocument doc = new XmlDocument();
                XmlNode docNode = doc.CreateXmlDeclaration("1.0", "UTF-8", null);
                doc.AppendChild(docNode);

                // report node
                XmlNode reportNode = doc.CreateElement("report");

                XmlAttribute reportId = doc.CreateAttribute("id");
                reportId.Value = id;
                reportNode.Attributes.Append(reportId);

                XmlAttribute reportTimestamp = doc.CreateAttribute("timestamp");
                reportTimestamp.Value = timeStamp.ToString("yyyyMMddTHHmmss+04");
                reportNode.Attributes.Append(reportTimestamp);

                doc.AppendChild(reportNode);

                // data node
                XmlNode dataNode = doc.CreateElement("data");

                XmlAttribute dataLane = doc.CreateAttribute("lane");
                dataLane.Value = "1";
                dataNode.Attributes.Append(dataLane);

                XmlAttribute dataDirection = doc.CreateAttribute("direction");
                dataDirection.Value = direction;
                dataNode.Attributes.Append(dataDirection);

                XmlAttribute dataSensor = doc.CreateAttribute("sensor");
                dataSensor.Value = "video_image_type_jpg";
                dataNode.Attributes.Append(dataSensor);

                reportNode.AppendChild(dataNode);

                // value node
                XmlNode valueNode = doc.CreateElement("value");
                valueNode.InnerText = Convert.ToBase64String(byteFrame);
                dataNode.AppendChild(valueNode);

                XmlWriterSettings xws = new XmlWriterSettings { OmitXmlDeclaration = false };
                using (XmlWriter xw = XmlWriter.Create(filePath, xws))
                {
                    doc.Save(xw);
                }
            }
        }

        /// <summary>
        /// Чтение конфигурационного xml файла и занесение данных в cameras
        /// </summary>
        private static void MinimaxDictionaryInitialization()
        {
            XmlSerializer serializer = new XmlSerializer(typeof(CameraCollection));
            StreamReader reader = new StreamReader(Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName) + "/configuration.xml");
            cameras = (CameraCollection)serializer.Deserialize(reader);
            reader.Close();
        }

        /// <summary>
        /// Получение идентификатора Минимакса и направления из конфигурационного файла
        /// </summary>
        /// <param name="logicalId">идентификатор камеры на сервере Avigilon</param>
        /// <param name="id">Идентификатор минимакса</param>
        /// <param name="direction">Направление</param>
        private static void GetIdAndDirection(uint logicalId, out string id, out string direction)
        {
            id = "";
            direction = "";

            foreach (Camera camera in cameras.Car)
            {
                if (camera.serverIp == m_address.ToString() && camera.cameraId == logicalId)
                {
                    id = camera.id;
                    direction = camera.direction;
                    break;
                }
            }
        }

        static void Main(string[] args)
        {
            if (ParseCommandLine())
            {
                MinimaxDictionaryInitialization();

                string filePathFinished = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName) + "/video/" + m_address.ToString().Replace(".", "") + ".finished";

                if (!File.Exists(filePathFinished))
                {
                    InitAvigilon();

                    m_endPoint = new IPEndPoint(m_address, m_controlCenter.DefaultNvrPortNumber);
                    AvgError result = m_controlCenter.AddNvr(m_endPoint);

                    if (ErrorHelper.IsError(result))
                    {
                        Console.WriteLine("An error occurred while adding the NVR." + m_endPoint.Address);
                    }

                    DateTime waitEnd = DateTime.Now + new TimeSpan(0, 0, 10);
                    INvr nvr = null;
                    while (DateTime.Now < waitEnd && nvr == null)
                    {
                        nvr = m_controlCenter.GetNvr(m_endPoint.Address);
                        if (nvr == null)
                        {
                            Thread.Sleep(500);
                        }
                    }

                    if (nvr == null)
                    {
                        Console.WriteLine("An error occurred while connecting to the NVR.");
                    }
                    else
                    {
                        LoginResult loginResult = nvr.Login(m_userName, m_password);
                        if (loginResult != 0)
                        {
                            Console.WriteLine("Failed to login to NVR: " + loginResult);
                        }
                        else
                        {
                            waitEnd = DateTime.Now + new TimeSpan(0, 0, 10);

                            List<IDevice> devices = new List<IDevice>();
                            while (DateTime.Now < waitEnd)
                            {
                                devices = nvr.Devices;

                                if (devices.Count > 0)
                                {
                                    break;
                                }

                                Thread.Sleep(500);
                            }

                            bool deviceConnectedExists = false;

                            // Получен список камер. Начинаем получение скриншотов
                            foreach (IDevice device in devices)
                            {
                                if (device.Connected)
                                {
                                    if (!deviceConnectedExists)
                                    {
                                        deviceConnectedExists = true;
                                    }

                                    uint logicalId = device.Entities.FirstOrDefault().LogicalId;
                                    IEntityCamera camera = (IEntityCamera)device.GetEntityByLogicalId(logicalId);

                                    if (camera == null)
                                    {
                                        Console.WriteLine("The given camera with LogicalId {0} is not connected to the NVR.", logicalId);
                                    }
                                    else
                                    {
                                        IStreamGroup streamGroup = m_controlCenter.CreateStreamGroup(PlaybackMode.Live);

                                        if (m_controlCenter.CreateStreamCallback(camera, streamGroup, MediaCoding.Jpeg, out IStreamCallback stream) == AvgError.Success)
                                        {
                                            stream.OutputSize = new Size(2048, 1536);
                                            stream.Overlays = Overlay.ImageTime;
                                            stream.Enable = true;
                                            IFrame frame = stream.GetFrame(new TimeSpan(0, 1, 0));
                                            DateTime timeStamp = DateTime.Now;

                                            byte[] byteFrame = frame.GetAsArray();

                                            stream.Enable = false;

                                            CreateXmlFile(byteFrame, logicalId, timeStamp);
                                        }
                                    }
                                }
                            }
                            
                            // отметка об окончании экспорта xml файлов при их наличии
                            if (deviceConnectedExists)
                            {
                                File.Create(filePathFinished);
                            }
                        }
                    }

                    m_controlCenter?.Dispose();
                    m_sdk.Shutdown();
                }
            }
        }
    }
}

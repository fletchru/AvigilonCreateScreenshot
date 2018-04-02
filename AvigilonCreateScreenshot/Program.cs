using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Xml;
using AvigilonDotNet;

namespace AvigilonCreateScreenshot
{
    public class MinimaxDictionary
    {
        public int serverId;
        public uint cameraId;
        public string id;
        public string direction;
    }

    class Program
    {
        private static AvigilonSdk m_sdk;
        private static IAvigilonControlCenter m_controlCenter;
        private static IPEndPoint m_endPoint;
        private static IPAddress m_address;
        private static string m_userName = "";
        private static string m_password = "";

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
        /// Словарь идентификаторов Минимакса
        /// </summary>
        /// <param name="logicalId">идентификатор камеры на сервере Avigilon</param>
        /// <param name="id">Идентификатор минимакса</param>
        /// <param name="direction">Направление</param>
        private static void GetIdAndDirection(uint logicalId, out string id, out string direction)
        {
            id = "";
            direction = "";

            MinimaxDictionary[] minimaxDictionaryArray = new MinimaxDictionary[32];

            minimaxDictionaryArray[0] = new MinimaxDictionary { serverId = 1, cameraId = 10, id = "30403", direction = "backward" };
            minimaxDictionaryArray[1] = new MinimaxDictionary { serverId = 1, cameraId = 10, id = "30404", direction = "forward" };
            minimaxDictionaryArray[2] = new MinimaxDictionary { serverId = 1, cameraId = 10, id = "30434", direction = "forward" };
            minimaxDictionaryArray[3] = new MinimaxDictionary { serverId = 1, cameraId = 10, id = "30405", direction = "forward" };
            minimaxDictionaryArray[4] = new MinimaxDictionary { serverId = 1, cameraId = 10, id = "30406", direction = "forward" };
            minimaxDictionaryArray[5] = new MinimaxDictionary { serverId = 1, cameraId = 10, id = "30407", direction = "forward" };
            minimaxDictionaryArray[6] = new MinimaxDictionary { serverId = 1, cameraId = 10, id = "30408", direction = "forward" };
            minimaxDictionaryArray[7] = new MinimaxDictionary { serverId = 1, cameraId = 10, id = "30409", direction = "backward" };
            minimaxDictionaryArray[8] = new MinimaxDictionary { serverId = 1, cameraId = 10, id = "30410", direction = "forward" };
            minimaxDictionaryArray[9] = new MinimaxDictionary { serverId = 1, cameraId = 10, id = "30411", direction = "backward" };
            minimaxDictionaryArray[10] = new MinimaxDictionary { serverId = 1, cameraId = 10, id = "30412", direction = "forward" };
            minimaxDictionaryArray[11] = new MinimaxDictionary { serverId = 1, cameraId = 10, id = "30413", direction = "backward" };
            minimaxDictionaryArray[12] = new MinimaxDictionary { serverId = 1, cameraId = 10, id = "30414", direction = "forward" };
            minimaxDictionaryArray[13] = new MinimaxDictionary { serverId = 1, cameraId = 10, id = "30415", direction = "forward" };
            minimaxDictionaryArray[14] = new MinimaxDictionary { serverId = 1, cameraId = 10, id = "30416", direction = "forward" };
            minimaxDictionaryArray[15] = new MinimaxDictionary { serverId = 1, cameraId = 10, id = "30417", direction = "forward" };
            minimaxDictionaryArray[16] = new MinimaxDictionary { serverId = 1, cameraId = 10, id = "30418", direction = "forward" };
            minimaxDictionaryArray[17] = new MinimaxDictionary { serverId = 1, cameraId = 10, id = "30419", direction = "forward" };
            minimaxDictionaryArray[18] = new MinimaxDictionary { serverId = 1, cameraId = 10, id = "30420", direction = "backward" };
            minimaxDictionaryArray[19] = new MinimaxDictionary { serverId = 1, cameraId = 10, id = "30421", direction = "forward" };
            minimaxDictionaryArray[20] = new MinimaxDictionary { serverId = 1, cameraId = 10, id = "30422", direction = "forward" };
            minimaxDictionaryArray[21] = new MinimaxDictionary { serverId = 1, cameraId = 10, id = "30423", direction = "forward" };
            minimaxDictionaryArray[22] = new MinimaxDictionary { serverId = 1, cameraId = 10, id = "30424", direction = "forward" };
            minimaxDictionaryArray[23] = new MinimaxDictionary { serverId = 1, cameraId = 10, id = "30425", direction = "backward" };
            minimaxDictionaryArray[24] = new MinimaxDictionary { serverId = 1, cameraId = 10, id = "30426", direction = "backward" };
            minimaxDictionaryArray[25] = new MinimaxDictionary { serverId = 1, cameraId = 10, id = "30427", direction = "forward" };
            minimaxDictionaryArray[26] = new MinimaxDictionary { serverId = 1, cameraId = 10, id = "30428", direction = "forward" };
            minimaxDictionaryArray[27] = new MinimaxDictionary { serverId = 1, cameraId = 10, id = "30429", direction = "backward" };
            minimaxDictionaryArray[28] = new MinimaxDictionary { serverId = 1, cameraId = 10, id = "30430", direction = "forward" };
            minimaxDictionaryArray[29] = new MinimaxDictionary { serverId = 1, cameraId = 10, id = "30431", direction = "forward" };
            minimaxDictionaryArray[30] = new MinimaxDictionary { serverId = 1, cameraId = 10, id = "30432", direction = "forward" };
            minimaxDictionaryArray[31] = new MinimaxDictionary { serverId = 1, cameraId = 10, id = "30433", direction = "backward" };

            int serverId = 0;
            if (m_address.ToString() == "172.16.10.115")
            {
                serverId = 1;
            }
            else if (m_address.ToString() == "127.0.0.1")
            {
                serverId = 2;
            }

            foreach (MinimaxDictionary minimaxDictionary in minimaxDictionaryArray)
            {
                if (minimaxDictionary.serverId == serverId && minimaxDictionary.cameraId == logicalId)
                {
                    id = minimaxDictionary.id;
                    direction = minimaxDictionary.direction;
                    break;
                }
            }
        }

        static void Main(string[] args)
        {
            if (ParseCommandLine())
            {
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

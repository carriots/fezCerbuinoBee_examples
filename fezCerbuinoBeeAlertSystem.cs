using System;
using System.Net;
using System.IO;
using System.Text;
using System.Collections;
using System.Threading;

using Microsoft.SPOT;
using Microsoft.SPOT.Presentation;
using Microsoft.SPOT.Presentation.Controls;
using Microsoft.SPOT.Presentation.Media;
using Microsoft.SPOT.Presentation.Shapes;
using Microsoft.SPOT.Touch;
using Microsoft.SPOT.Net;

using Gadgeteer.Networking;
using GT = Gadgeteer;
using GTM = Gadgeteer.Modules;
using Microsoft.SPOT.Hardware;
using Microsoft.SPOT.Net.NetworkInformation;


namespace YOUR NAMESPACE HERE
{
    public partial class Program
    {
        static String carriotsDevice = "defaultDevice@myusername"; //TO BE REPLACED with your Device's ID develope
        static String apikey = "98346673a637...d83045425407ab4"; // TO BE REPLACED with your Carriots APIKEY
        static AnalogInput input;
        private GT.Timer gcTimer = new GT.Timer(2000); // Set the timer to tick every 2 seconds
        static int ON = 1;
        static int OFF = 2;

        int lights = OFF;
        int newLights = OFF;

        double val = 0;

        // This method is run when the mainboard is powered up or reset.   
        void ProgramStarted()
        {
            // Wait for the Ethernet connection
            while (true)
            {
                IPAddress ip = IPAddress.GetDefaultLocalAddress();
                Debug.Print(ip.ToString());
                if (ip != IPAddress.Any) break;
                Thread.Sleep(1000);
            }
            // Set the Analog Input to Channel 7 (A3 input on the Cerbuino Bee board)
            input = new AnalogInput(Cpu.AnalogChannel.ANALOG_7);
            // Start the timer
            gcTimer.Tick += timer_Tick;
            gcTimer.Start();
        }

        void timer_Tick(GT.Timer timer)
        {
            val = input.Read();
            if (val != 0)
            {
                Debug.Print("" + val);
                // If the value read by the sensor is greater than .02 the lights are off
                if (val > .02)
                {
                    newLights = OFF;
                }
                else
                {
                    newLights = ON;
                }
                // If the newLights status has changed since the last reading, send a stream to Carriots
                if (lights != newLights)
                {
                    Debug.Print("Send stream");
                    lights = newLights;
                    sendStream(lights);
                }
            }
        }

        public void sendStream(int changedLight)
        {
            String txt = "OFF";
            if (changedLight == ON)
            {
                txt = "ON";
            }
            // Set the string that will be sent to Carriots
            string myText = "{\"protocol\":\"v2\",\"device\":\""+carriotsDevice+"\",\"at\":\"now\",\"data\":{\"lights\":\"" + txt + "\"}}";

            // Create an HttpWebRequest to the Carriots server
            HttpWebRequest request = HttpWebRequest.Create("http://api.carriots.com/streams/") as HttpWebRequest;

            // Define the necessary headers to accompany the petition
            request.Method = "POST";
            request.Headers.Add("Carriots.apikey: " + apikey);
            request.ContentType = "application/json";
            request.Accept = "application/json";
            request.ContentLength = myText.Length;
            request.UserAgent = "Carriots-client";
            request.AllowWriteStreamBuffering = true;

            // Send the stream 
            StreamWriter streamPost = new StreamWriter(request.GetRequestStream());
            streamPost.Write(myText);

            streamPost.Flush();
            streamPost.Close();

            // Get a response from the server.
            WebResponse resp = null;
            try
            {
                resp = request.GetResponse();
            }
            catch (Exception e)
            {
                Debug.Print(e.ToString());
            }
            // Get the Carriots response stream
            if (resp != null)
            {
                Stream respStream = resp.GetResponseStream();
                string page = null;
                byte[] byteData = new byte[4096];
                char[] charData = new char[4096];
                int bytesRead = 0;
                Decoder UTF8decoder = System.Text.Encoding.UTF8.GetDecoder();
                int totalBytes = 0;

                // allow 5 seconds for reading the stream
                respStream.ReadTimeout = 5000;

                // If we know the content length, read exactly that amount of 
                // data; otherwise, read until there is nothing left to read.
                if (resp.ContentLength != -1)
                {
                    for (int dataRem = (int)resp.ContentLength; dataRem > 0; )
                    {
                        Thread.Sleep(500);
                        bytesRead =
                            respStream.Read(byteData, 0, byteData.Length);
                        if (bytesRead == 0)
                        {
                            //Debug.Print("Error: Received " +(resp.ContentLength - dataRem) + " Out of " +resp.ContentLength);
                            break;
                        }
                        dataRem -= bytesRead;

                        // Convert from bytes to chars, and add to the page 
                        // string.
                        int byteUsed, charUsed;
                        bool completed = false;
                        totalBytes += bytesRead;
                        UTF8decoder.Convert(byteData, 0, bytesRead, charData, 0,
                            bytesRead, true, out byteUsed, out charUsed,
                            out completed);
                        page = page + new String(charData, 0, charUsed);
                    }

                    page = new String(System.Text.Encoding.UTF8.GetChars(byteData));
                }
                else
                {
                    // Read until the end of the data is reached.
                    while (true)
                    {
                        // If the Read method times out, it throws an exception
                        try
                        {
                            Thread.Sleep(500);
                            bytesRead =
                                respStream.Read(byteData, 0, byteData.Length);
                        }
                        catch (Exception)
                        {
                            bytesRead = 0;
                        }

                        // Zero bytes indicates the connection closed by the server
                        if (bytesRead == 0)
                        {
                            break;
                        }

                        int byteUsed, charUsed;
                        bool completed = false;
                        totalBytes += bytesRead;
                        UTF8decoder.Convert(byteData, 0, bytesRead, charData, 0,
                            bytesRead, true, out byteUsed, out charUsed,
                            out completed);
                        page = page + new String(charData, 0, charUsed);
                        //Debug.Print("Bytes Read Now: " + bytesRead +" Total: " + totalBytes);
                    }

                    //Debug.Print("Total bytes downloaded in message body : "+ totalBytes);
                }

                // Display the page results.
                Debug.Print(page);

                // Close the response stream. 
                resp.Close();
            }
        }

    }

}

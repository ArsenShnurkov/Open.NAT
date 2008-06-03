using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using Mono.Nat.Pmp;

namespace Mono.Nat
{
    internal class PmpSearcher : ISearcher
    {
        private int timeout;
        private DateTime nextSearch;
        private IPEndPoint searchEndpoint;
        public event EventHandler<DeviceEventArgs> DeviceFound;
        public event EventHandler<DeviceEventArgs> DeviceLost;

        public PmpSearcher()
        {
            timeout = 250;
            searchEndpoint = new IPEndPoint(IPAddress.Parse("192.168.0.1"), PmpConstants.Port);
        }

        public void Search(System.Net.Sockets.UdpClient client)
        {
            // Sort out the time for the next search first. The spec says the 
            // timeout should double after each attempt. Once it reaches 64 seconds
            // (and that attempt fails), assume no devices available
            nextSearch = DateTime.Now.AddMilliseconds(timeout);
            timeout *= 2;

            // We've tried 9 times as per spec, try searching again in 5 minutes
            if (timeout == 128 * 1000)
            {
                timeout = 250;
                nextSearch = DateTime.Now.AddMinutes(10);
                return;
            }

            // The nat-pmp search message. Must be sent to GatewayIP:53531
            byte[] buffer = new byte[] { PmpConstants.Version, PmpConstants.OperationCode };
            client.Send(buffer, buffer.Length, searchEndpoint);
        }

        public IPEndPoint SearchEndpoint
        {
            get { return searchEndpoint; }
        }

        public void Handle(byte[] response, IPEndPoint endpoint)
        {
            if (!endpoint.Address.Equals(searchEndpoint.Address))
                return;
            if (response.Length != 12)
                return;
            if (response[0] != PmpConstants.Version)
                return;
            if (response[1] != PmpConstants.ServerNoop)
                return;
            int errorcode = IPAddress.NetworkToHostOrder(BitConverter.ToInt16(response, 2));
            if (errorcode != 0)
                Console.WriteLine("Non zero error: {0}", errorcode);

            IPAddress publicIp = new IPAddress(new byte[] { response[8], response[9], response[10], response[11] });
            nextSearch = DateTime.Now.AddMinutes(5);
            timeout = 250;
            OnDeviceFound(new DeviceEventArgs(new PmpNatDevice(endpoint.Address, publicIp)));
        }

        public DateTime NextSearch
        {
            get { return nextSearch; }
        }
        private void OnDeviceFound(DeviceEventArgs args)
        {
            if (DeviceFound != null)
                DeviceFound(this, args);
        }
    }
}
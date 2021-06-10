using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Linq;

using ArtNet.Sockets;
using ArtNet.Packets;
using System;
using System.Threading.Tasks;
using ArtNet.Enums;

namespace ArtNet
{
    public class DmxController : IDisposable
    {
        public bool UseBroadcast { get; set; }
        public IPAddress SubnetMask { get; set; } = null;
        public string RemoteIP { get; set; } = "localhost";
        public List<UniverseDevices> Universes { get; set; }
        public bool IsServer { get; set; }

        private IPEndPoint remote;
        private IPAddress bindAddress;
        private ArtNetSocket artnet;
        private ArtNetDmxPacket latestReceivedDMX;
        private ArtNetDmxPacket dmxToSend;
        private byte[] dmxData;        
        private Dictionary<int, byte[]> dmxDataMap;
        private bool newPacket;
        private bool isRunning;

        public DmxController(List<UniverseDevices> universes, string remoteIp = "localhost", bool isServer = true, bool useBroadcast=false)
        {
            UseBroadcast = useBroadcast;
            RemoteIP = remoteIp;
            Universes = universes ?? new List<UniverseDevices>();
            dmxToSend = new ArtNetDmxPacket();
            latestReceivedDMX = new ArtNetDmxPacket();
            IsServer = isServer;

            OnValidate();
            Start();
        }
        

        public void Send()
        {
            if (UseBroadcast && IsServer)
                artnet.Send(dmxToSend);
            else
                artnet.Send(dmxToSend, remote);
        }

        public void Send(short universe, byte[] dmxData)
        {
            dmxToSend.Universe = universe;
            System.Buffer.BlockCopy(dmxData, 0, dmxToSend.DmxData, 0, dmxData.Length);

            if (UseBroadcast && IsServer)
                artnet.Send(dmxToSend);
            else
                artnet.Send(dmxToSend, remote);
        }

        private void OnValidate()
        {
            foreach (var u in Universes)
                u.Initialize();
        }

        private void Start()
        {
            artnet = new ArtNetSocket();
            if (IsServer)
            {
                bindAddress = FindFromHostName("localhost");
                artnet.Open(bindAddress, SubnetMask);
                Console.WriteLine($"Listening on port {ArtNetSocket.Port}");
            }
            // If you set the subnet mask, it will set the address that you will not send to yourself (convenient!)
            // But debugging gets messy
            dmxToSend.DmxData = new byte[512];
            dmxDataMap = new Dictionary<int, byte[]>();
            newPacket = true;

            artnet.NewPacket += (object sender, NewPacketEventArgs<ArtNetPacket> e) =>
            {
                newPacket = true;
                if (e.Packet.OpCode == ArtNet.Enums.ArtNetOpCodes.Dmx)
                {
                    var packet = latestReceivedDMX = e.Packet as ArtNetDmxPacket;

                    if (packet.DmxData != dmxData)
                        dmxData = packet.DmxData;

                    var universe = packet.Universe;
                    if (dmxDataMap.ContainsKey(universe))
                        dmxDataMap[universe] = packet.DmxData;
                    else
                        dmxDataMap.Add(universe, packet.DmxData);
                } else if (e.Packet.OpCode == ArtNetOpCodes.Poll)
                {
                    var packet = e.Packet as ArtNet.Packets.ArtPollPacket;
                    var source = e.Source;
                    
                    Console.WriteLine($"IN > POLL: {packet.Protocol} V: {packet.Version}, TalkToMe:{packet.TalkToMe}");

                    var pollReply = new ArtNet.Packets.ArtPollReplyPacket();
                    pollReply.BindIndex = 1;
                    pollReply.BindIpAddress = bindAddress.GetAddressBytes();
                    pollReply.GoodInput = new byte[4];
                    pollReply.GoodOutput = new byte[4];
                    pollReply.IpAddress = bindAddress.GetAddressBytes();
                    pollReply.LongName = "ACT.ArtNetStreamer";
                    pollReply.MacAddress = new byte[6]; //get local mac address?
                    pollReply.NodeReport = "#0001 [0000] ACT.ArtNetStreamer Online";
                    pollReply.Port = ArtNetSocket.Port;
                    pollReply.PortCount = 1;
                    pollReply.PortTypes = new byte[4];
                    pollReply.ShortName = bindAddress.ToString();
                    pollReply.Status = PollReplyStatus.None;

                    artnet.Send(pollReply, source);

                }
                else if (e.Packet.OpCode == ArtNetOpCodes.PollReply)
                {
                    var packet = e.Packet as ArtNet.Packets.ArtPollReplyPacket;

                    Console.WriteLine($"IN > POLLREPLY: {packet.Protocol} V: {packet.Version}");
                    Console.WriteLine(packet.ToString());
                }
                else
                {
                    Console.WriteLine($"Unhandled packet: OpCode({e.Packet.OpCode}).");
                }
            };

            if (!UseBroadcast || !IsServer) 
                remote = new IPEndPoint(FindFromHostName(RemoteIP), ArtNetSocket.Port);

            

            _ = UpdateThread();
        }

        private async Task UpdateThread()
        {
            isRunning = true;
            while (isRunning)
            {
                try
                {
                    Update();
                }catch(Exception ex)
                {
                    Console.WriteLine("Exception in UpdateThread: {0}", ex);
                    isRunning = false;
                }
                await Task.Delay(2);
            }

            Dispose();
        }

        private void Update()
        {
            if (!newPacket) return;
            newPacket = false;

            var keys = dmxDataMap.Keys.ToArray();

            for (var i = 0; i < keys.Length; i++)
            {
                var universe = keys[i];
                var dmxData = dmxDataMap[universe];
                if (dmxData == null)
                    continue;

                var universeDevices = Universes.Where(u => u.UniverseIndex == universe).FirstOrDefault();
                if (universeDevices != null)
                    foreach (var d in universeDevices.Devices)
                        d.SetData(dmxData.Skip(d.StartChannelIx).Take(d.NumChannels).ToArray());

                dmxDataMap[universe] = null;
            }
        }

        static IPAddress FindFromHostName(string hostname)
        {
            var address = IPAddress.None;
            try
            {
                if (IPAddress.TryParse(hostname, out address))
                    return address;

                var addresses = Dns.GetHostAddresses(hostname);
                for (var i = 0; i < addresses.Length; i++)
                {
                    if (addresses[i].AddressFamily == AddressFamily.InterNetwork)
                    {
                        address = addresses[i];
                        break;
                    }
                }
            }
            catch (System.Exception e)
            {
                Console.WriteLine(
                    "Failed to find IP for :\n host name = {0}\n exception={1}",
                    hostname, e);
            }
            return address;
        }

        public void Dispose()
        {
            artnet.Close();
            isRunning = false;
        }
    }
}
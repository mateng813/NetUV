﻿// Copyright (c) Johnny Z. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace NetUV.Core.Tests.Performance
{
    using System;
    using System.Net;
    using System.Text;
    using NetUV.Core.Buffers;
    using NetUV.Core.Channels;
    using NetUV.Core.Handles;

    sealed class EchoServer : IDisposable
    {
        const int MaximumBacklogSize = 1000;
        readonly ScheduleHandle server;

        public EchoServer(HandleType handleType)
        {
            this.Loop = new Loop();

            switch (handleType)
            {
                case HandleType.Udp:
                    this.server = this.Loop
                        .CreateUdp()
                        .Bind(TestHelper.AnyEndPoint)
                        .ReceiveStart(OnReceive);
                    break;
                case HandleType.Tcp:
                    this.server = this.Loop
                        .CreateTcp()
                        .SimultaneousAccepts(true)
                        .Listen(TestHelper.LoopbackEndPoint, this.OnConnection, MaximumBacklogSize);
                    break;
                case HandleType.Pipe:
                    this.server = this.Loop
                        .CreatePipe()
                        .Listen(TestHelper.LocalPipeName, this.OnConnection, MaximumBacklogSize);
                    break;
                default:
                    throw new InvalidOperationException($"{handleType} not supported.");
            }
        }

        internal Loop Loop { get; }

        static void OnReceive(Udp udp, IDatagramReadCompletion completion)
        {
            if (completion.Error != null)
            {
                completion.Dispose();
                Console.WriteLine($"{nameof(EchoServer)} receive error {completion.Error}");
                udp.ReceiveStop();
                udp.CloseHandle(OnClose);
                return;
            }

            ReadableBuffer data = completion.Data;
            string message = data.Count > 0 ? data.ReadString(data.Count, Encoding.UTF8) : null;
            data.Dispose();

            IPEndPoint remoteEndPoint = completion.RemoteEndPoint;
            if (string.IsNullOrEmpty(message) 
                || remoteEndPoint == null)
            {
                return;
            }

            WritableBuffer buffer = WritableBuffer.From(Encoding.UTF8.GetBytes(message));
            udp.QueueSend(buffer, remoteEndPoint, OnSendCompleted);
        }

        static void OnSendCompleted(Udp udp, Exception exception)
        {
            if (exception != null)
            {
                udp.CloseHandle(OnClose);
                Console.WriteLine($"{nameof(EchoServer)} send error {exception}");
            }
        }

        void OnConnection<T>(T client, Exception error) 
            where T : StreamHandle
        {
            if (error != null)
            {
                Console.WriteLine($"{nameof(EchoServer)} client connection failed, {error}");
                client.CloseHandle(OnClose);
            }
            else
            {
                client.CreateStream().Subscribe(this.OnNext, OnError, OnComplete);
            }
        }

        void OnNext(IStream stream, ReadableBuffer data)
        {
            string message = data.Count > 0 ? data.ReadString(data.Count, Encoding.UTF8) : null;
            data.Dispose();

            if (string.IsNullOrEmpty(message))
            {
                return;
            }

            //
            // Scan for the letter Q which signals that we should quit the server.
            // If we get QS it means close the stream.
            //
            if (message.StartsWith("Q"))
            {
                if (message.EndsWith("QS"))
                {
                    stream.Handle.CloseHandle(OnClose);
                }
                else
                {
                    this.CloseServer();

                }
            }
            else
            {
                WritableBuffer buffer = WritableBuffer.From(Encoding.UTF8.GetBytes(message));
                stream.Write(buffer, OnWriteCompleted);
            }
        }

        public void CloseServer()
        {
            var serverStream = this.server as ServerStream;
            if (serverStream != null)
            {
                serverStream.CloseHandle(OnClose);
            }
            else
            {
                ((Udp)this.server).CloseHandle(OnClose);
            }
        }

        static void OnWriteCompleted(IStream stream, Exception error)
        {
            if (error == null)
            {
                return;
            }

            Console.WriteLine($"{nameof(EchoServer)} write failed, {error}");
            stream.Handle.CloseHandle(OnClose);
        }

        static void OnError(IStream stream, Exception exception)
        {
            Console.WriteLine($"{nameof(EchoServer)} read error {exception}");
            stream.Shutdown(OnShutdown);
        }

        static void OnShutdown(IStream handle, Exception exception) => handle.Handle.CloseHandle(OnClose);

        static void OnClose(ScheduleHandle handle) => handle.Dispose();

        static void OnComplete(IStream stream) => stream.Handle.CloseHandle(OnClose);

        public void Dispose()
        {
            this.server.Dispose();
            this.Loop.Dispose();
        }
    }
}

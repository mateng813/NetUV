﻿// Copyright (c) Johnny Z. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace NetUV.Core.Tests.Buffers
{
    using System;
    using NetUV.Core.Buffers;
    using Xunit;

    public abstract class AbstractPooledByteBufferTests : AbstractByteBufferTests
    {
        internal abstract IByteBuffer Alloc(int length, int maxCapacity);

        internal override IByteBuffer NewBuffer(int length, int maxCapacity)
        {
            IByteBuffer buffer = this.Alloc(length, maxCapacity);

            // Testing if the writerIndex and readerIndex are correct when allocate and also after we reset the mark.
            Assert.Equal(0, buffer.WriterIndex);
            Assert.Equal(0, buffer.ReaderIndex);
            buffer.ResetReaderIndex();
            buffer.ResetWriterIndex();
            Assert.Equal(0, buffer.WriterIndex);
            Assert.Equal(0, buffer.ReaderIndex);

            return buffer;
        }

        [Fact]
        public void EnsureWritableWithEnoughSpaceShouldNotThrow()
        {
            IByteBuffer buf = this.NewBuffer(1, 10);
            buf.EnsureWritable(3);
            Assert.True(buf.WritableBytes >= 3);
            buf.Release();
        }

        [Fact]
        public void EnsureWritableWithNotEnoughSpaceShouldThrow()
        {
            IByteBuffer buf = this.NewBuffer(1, 10);
            Assert.Throws<IndexOutOfRangeException>(() => buf.EnsureWritable(11));
            buf.Release();
        }
    }
}

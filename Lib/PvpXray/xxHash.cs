// This file was copied and modified from
// https://github.com/xoofx/smash/blob/v0.3.0/src/Smash/xxHash.cs

// Copyright (c) 2017, Alexandre Mutel
// All rights reserved.
//
// Redistribution and use in source and binary forms, with or without modification
// , are permitted provided that the following conditions are met:
//
// 1. Redistributions of source code must retain the above copyright notice, this
//    list of conditions and the following disclaimer.
//
// 2. Redistributions in binary form must reproduce the above copyright notice,
//    this list of conditions and the following disclaimer in the documentation
//    and/or other materials provided with the distribution.
//
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
// ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
// DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE
// FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL
// DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR
// SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER
// CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY,
// OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
// OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

/*
*  xxHash - Fast Hash algorithm
*  Copyright (C) 2012-2016, Yann Collet
*
*  BSD 2-Clause License (http://www.opensource.org/licenses/bsd-license.php)
*
*  Redistribution and use in source and binary forms, with or without
*  modification, are permitted provided that the following conditions are
*  met:
*
*  * Redistributions of source code must retain the above copyright
*  notice, this list of conditions and the following disclaimer.
*  * Redistributions in binary form must reproduce the above
*  copyright notice, this list of conditions and the following disclaimer
*  in the documentation and/or other materials provided with the
*  distribution.
*
*  THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
*  "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
*  LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
*  A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT
*  OWNER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
*  SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
*  LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
*  DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
*  THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
*  (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
*  OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
*
*  You can contact the author at :
*  - xxHash homepage: http://www.xxhash.com
*  - xxHash source repository : https://github.com/Cyan4973/xxHash
*/

using System;
using System.Runtime.CompilerServices;

// ReSharper disable InconsistentNaming
using U64 = System.UInt64;
using U32 = System.UInt32;

namespace PvpXray
{
    /// <summary>
    /// The xxHash method. Use <see cref="Create64"/>.
    /// </summary>
    static class xxHash
    {
        [MethodImpl((MethodImplOptions)0x100)] // Aggressive inlining for all .NETs
        public static Hash64 Create64(ulong seed)
        {
            return new Hash64(seed);
        }

        /// <summary>
        /// xxHash for computing a 64bit hash.
        /// </summary>
        public struct Hash64
        {
            const U64 PRIME64_1 = 11400714785074694791UL;
            const U64 PRIME64_2 = 14029467366897019727UL;
            const U64 PRIME64_3 = 1609587929392839161UL;
            const U64 PRIME64_4 = 9650029242287828579UL;
            // BUG: Prime #5 is unused. It is SUPPOSED to be used for inputs that are not a multiple
            // of 32 bytes. Which explains why this xxHash implementation is broken for such inputs.
            const U64 PRIME64_5 = 2870177450012600261UL;

            U64 _v1;
            U64 _v2;
            U64 _v3;
            U64 _v4;

            ulong _writeCount;
            ulong _length;

            [MethodImpl((MethodImplOptions)0x100)] // Aggressive inlining for all .NETs
            internal Hash64(ulong seed)
            {
                // NOTE: Duplicated with Reset method, keep in sync!
                _v1 = seed + PRIME64_1 + PRIME64_2;
                _v2 = seed + PRIME64_2;
                _v3 = seed + 0;
                _v4 = seed - PRIME64_1;
                _writeCount = 0;
                _length = 0;
            }

            [MethodImpl((MethodImplOptions)0x100)] // Aggressive inlining for all .NETs
            void Write(ulong input)
            {
                WriteInternal(input);
                _length += 8;
            }

            [MethodImpl((MethodImplOptions)0x100)] // Aggressive inlining for all .NETs
            void Write(byte input)
            {
                WriteInternal(input * PRIME64_1);
                _length += 1;
            }

            unsafe void Write(IntPtr buffer, ulong length)
            {
                if (buffer == IntPtr.Zero) throw new ArgumentNullException(nameof(buffer));
                var pBuffer = (byte*)(void*)buffer;

                const ulong FetchSize = sizeof(ulong) * 4;
                while (length >= FetchSize)
                {
                    var pulong = (ulong*)pBuffer;
                    Write(pulong[0]);
                    Write(pulong[1]);
                    Write(pulong[2]);
                    Write(pulong[3]);
                    pBuffer += FetchSize;
                    length -= FetchSize;
                }

                while (length >= 8)
                {
                    Write(*(ulong*)pBuffer);
                    pBuffer += 8;
                    length -= 8;
                }

                while (length > 0)
                {
                    Write(*(byte*)pBuffer);
                    pBuffer += 1;
                    length -= 1;
                }
            }

            public unsafe void Write(byte[] buffer, int offset, int count)
            {
                if (buffer == null) throw new ArgumentNullException(nameof(buffer));
                if (offset < 0)
                    throw new ArgumentOutOfRangeException(nameof(offset), "offset cannot be < 0");
                if (count < 0)
                    throw new ArgumentOutOfRangeException(nameof(count), "count cannot be < 0");
                if (buffer.Length - offset < count)
                    throw new ArgumentException("Invalid offset + count > length");

                if (count == 0)
                {
                    return;
                }

                fixed (void* pByte = &buffer[offset])
                {
                    Write(new IntPtr(pByte), (ulong)count);
                }
            }

            [MethodImpl((MethodImplOptions)0x100)] // Aggressive inlining for all .NETs
            public ulong Compute()
            {
                // This part is slightly different from xxHash to use a generic algorithm
                // depending on the number of writes
                // Might not give the exact same result as we are feeding all states v1,v2,v3,v4
                // while xxHash calculates the hash differently for input buffer < 64/32/8/4 bytes
                var h64 = XXH_rotl64(_v1, 1);
                if (_writeCount >= 2)
                {
                    h64 += XXH_rotl64(_v2, 7);
                    if (_writeCount >= 3)
                    {
                        h64 += XXH_rotl64(_v3, 12);
                        if (_writeCount >= 4)
                        {
                            h64 += XXH_rotl64(_v4, 18);
                        }
                    }
                }

                h64 = XXH64_mergeRound(h64, _v1);
                if (_writeCount >= 2)
                {
                    h64 = XXH64_mergeRound(h64, _v2);
                    if (_writeCount >= 3)
                    {
                        h64 = XXH64_mergeRound(h64, _v3);
                        if (_writeCount >= 4)
                        {
                            h64 = XXH64_mergeRound(h64, _v4);
                        }
                    }
                }

                h64 += _length;

                h64 ^= h64 >> 33;
                h64 *= PRIME64_2;
                h64 ^= h64 >> 29;
                h64 *= PRIME64_3;
                h64 ^= h64 >> 32;

                return h64;
            }

            [MethodImpl((MethodImplOptions)0x100)] // Aggressive inlining for all .NETs
            void WriteInternal(ulong input)
            {
                switch (_writeCount & 3)
                {
                    case 0:
                        _v1 = XXH64_round(_v1, input);
                        break;
                    case 1:
                        _v2 = XXH64_round(_v2, input);
                        break;
                    case 2:
                        _v3 = XXH64_round(_v3, input);
                        break;
                    case 3:
                        _v4 = XXH64_round(_v4, input);
                        break;
                }
                _writeCount++;
            }

            [MethodImpl((MethodImplOptions)0x100)] // Aggressive inlining for all .NETs
            static U64 XXH_rotl64(U64 x, int r)
            {
                return (x << r) | (x >> (64 - r));
            }

            [MethodImpl((MethodImplOptions)0x100)] // Aggressive inlining for all .NETs
            static U64 XXH64_round(U64 acc, U64 input)
            {
                acc += input * PRIME64_2;
                acc = XXH_rotl64(acc, 31);
                acc *= PRIME64_1;
                return acc;
            }

            [MethodImpl((MethodImplOptions)0x100)] // Aggressive inlining for all .NETs
            static U64 XXH64_mergeRound(U64 acc, U64 val)
            {
                val = XXH64_round(0, val);
                acc ^= val;
                acc = acc * PRIME64_1 + PRIME64_4;
                return acc;
            }
        }
    }
}

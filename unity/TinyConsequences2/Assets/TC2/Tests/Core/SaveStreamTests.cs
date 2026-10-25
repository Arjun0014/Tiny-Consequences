using System;
using System.IO;
using NUnit.Framework;
using TC2.Core.Foundation;
using TC2.Core.Persistence;
using TC2.Core.Simulation;

namespace TC2.Tests.Core
{
    /// <summary>
    /// A save streamed (the memory pass, 14 §7): the world's state is hashed and deflated a chunk at a time as it is written, and inflated a chunk at
    /// a time as it is read - the save the same world, its payload hash and length as before, the buffers a chunk of it, a damaged save refused.
    /// </summary>
    [TestFixture]
    public class SaveStreamTests
    {
        static World Lived()
        {
            var w = TestWorlds.Make(7);
            w.AdvanceTo(w.Now + 3 * SimTime.Day);
            return w;
        }

        [Test]
        public void AStreamedSaveHashesItsStateAsBeforeAndLoadsTheSameWorld()
        {
            var w = Lived();
            var plain = SaveFile.PeekHeader(SaveFile.Save(w, 0, OfflineMode.Paused, compress: false));
            byte[] packed = SaveFile.Save(w, 0, OfflineMode.Paused);
            var h = SaveFile.PeekHeader(packed);
            Console.WriteLine("state " + h.UncompressedLength + " bytes, saved " + h.PayloadLength + "; payload hash " + h.PayloadHash.ToString("x16"));
            Assert.AreEqual(plain.UncompressedLength, h.UncompressedLength, "the whole state passed through");
            Assert.AreEqual(plain.PayloadHash, h.PayloadHash, "hashed as the state's bytes in one piece are");
            Assert.Less(h.PayloadLength, h.UncompressedLength, "deflated");
            var back = SaveFile.Load(packed, out _);
            Assert.AreEqual(w.StateHash(), back.StateHash(), "the same world");
            // and it lives on as the world it was saved from
            w.AdvanceTo(w.Now + SimTime.Day); back.AdvanceTo(back.Now + SimTime.Day);
            Assert.AreEqual(w.StateHash(), back.StateHash(), "it continues identically");
        }

        [Test]
        public void ASaveHoldsAChunkOfTheStateNotAllOfIt()
        {
            var w = Lived();
            var scratch = new SaveFile.Scratch();
            int len = SaveFile.Save(w, scratch, 0, OfflineMode.Paused);
            var h = SaveFile.PeekHeader(scratch.Out.ToArray());
            Console.WriteLine("state " + h.UncompressedLength + " bytes through a body of " + scratch.Body.Buffer.Length + "; the save " + len + " bytes");
            Assert.Greater(h.UncompressedLength, SaveFile.Chunk, "the premise: a state larger than a chunk");
            Assert.LessOrEqual(scratch.Body.Buffer.Length, SaveFile.Chunk, "the body never held the whole state");
            // the scratch sized for a loaded world keeps a chunk too, and saves the same bytes
            var sized = SaveFile.Scratch.For(h);
            int len2 = SaveFile.Save(w, sized, 0, OfflineMode.Paused);
            Assert.AreEqual(len, len2);
            Assert.LessOrEqual(sized.Body.Buffer.Length, SaveFile.Chunk);
            CollectionAssert.AreEqual(scratch.Out.ToArray(), sized.Out.ToArray(), "the same save whatever the buffers");
        }

        [Test]
        public void ADamagedSaveIsRefused()
        {
            var w = Lived();
            byte[] packed = SaveFile.Save(w, 0, OfflineMode.Paused);
            var h = SaveFile.PeekHeader(packed);
            int payloadAt = packed.Length - h.PayloadLength;
            // a byte of the deflated state changed: the inflater, the reading or the hash refuses it
            var bad = (byte[])packed.Clone();
            bad[payloadAt + h.PayloadLength / 2] ^= 0x5A;
            Assert.That(() => SaveFile.Load(bad, out _), Throws.Exception, "a damaged save does not load");
            // cut short: refused
            var cut = new byte[packed.Length - h.PayloadLength / 3];
            Buffer.BlockCopy(packed, 0, cut, 0, cut.Length);
            Assert.That(() => SaveFile.Load(cut, out _), Throws.Exception);
        }
    }
}

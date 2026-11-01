using System;

namespace TC2.Game
{
    /// <summary>
    /// (session 31: a person worth following) A small painted bust, the miniature in a chronicle's margin, from what the world knows of them: their age (a child's round
    /// face, grey hair and a lined brow), man or woman, their household's cloth, a first's mantle and circlet in the realm's colour, a sworn man's iron cap and the
    /// realm's colour on the shoulder; the rest (hair, skin, a beard, a coif) kept by their number, the same every time they are drawn; the dead in faded tones. Pure
    /// arithmetic with no engine in it (so it can be drawn and judged by itself); nothing here reads or writes the world.
    /// </summary>
    public static class PortraitArt
    {
        public struct Spec
        {
            public int Seed; public float Age; public bool Woman, Alive; public byte Rank;   // (Rank: 0 none, 1 the first of a place, 2 a sworn man)
            public float ClothR, ClothG, ClothB, RealmR, RealmG, RealmB;
        }

        struct C
        {
            public float R, G, B;
            public C(float r, float g, float b) { R = r; G = g; B = b; }
            public static C Lerp(C a, C b, float t) => new C(a.R + (b.R - a.R) * t, a.G + (b.G - a.G) * t, a.B + (b.B - a.B) * t);
            public C Dark(float k) => new C(R * (1f - k), G * (1f - k), B * (1f - k));
            public C Light(float k) => Lerp(this, new C(1f, 1f, 1f), k);
        }

        static float Hash(int seed, int salt)
        {
            uint x = (uint)seed * 2654435761u ^ (uint)salt * 40503u;
            x ^= x >> 13; x *= 1274126177u; x ^= x >> 16;
            return (x & 0xFFFFFF) / 16777216f;
        }

        static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
        static float Sat(float edge, float aa = 1.2f) => Clamp01(0.5f - edge / aa);   // (inside when the distance is negative: 1; outside: 0; soft over a pixel)
        static float Ell(float x, float y, float cx, float cy, float rx, float ry) { float dx = (x - cx) / rx, dy = (y - cy) / ry; return ((float)Math.Sqrt(dx * dx + dy * dy) - 1f) * Math.Min(rx, ry); }
        static float Box(float x, float y, float x0, float y0, float x1, float y1) { float dx = Math.Max(x0 - x, x - x1), dy = Math.Max(y0 - y, y - y1); return Math.Max(dx, dy); }

        /// <summary>Paints the bust into w x h pixels, rows from the top, as linear-free display colours (r, g, b, a per pixel in 0..1).</summary>
        public static void Paint(in Spec s, int w, int h, float[] rgba)
        {
            float U = w / 156f;   // (drawn in a 156 x 196 frame)
            float age = s.Age;
            float child = age < 6f ? 1f : age < 14f ? (14f - age) / 8f : 0f;   // (a child's head large on small shoulders)
            bool grownMan = !s.Woman && age >= 17f;
            float grey = Clamp01((age - 40f) / 30f), lined = Clamp01((age - 34f) / 36f);
            // what their number keeps
            float h0 = Hash(s.Seed, 1), h1 = Hash(s.Seed, 2), h2 = Hash(s.Seed, 3), h3 = Hash(s.Seed, 4), h4 = Hash(s.Seed, 5), h5 = Hash(s.Seed, 6);
            C[] hairs = { new C(0.20f, 0.13f, 0.08f), new C(0.32f, 0.20f, 0.11f), new C(0.47f, 0.30f, 0.15f), new C(0.64f, 0.45f, 0.22f), new C(0.12f, 0.09f, 0.07f), new C(0.52f, 0.22f, 0.10f) };
            C hairBase = hairs[(int)(h0 * hairs.Length) % hairs.Length];
            C hair = C.Lerp(hairBase, new C(0.80f, 0.79f, 0.76f), grey * grey);
            C skin = C.Lerp(new C(0.90f, 0.72f, 0.58f), new C(0.72f, 0.52f, 0.38f), h1 * 0.9f);
            skin = C.Lerp(skin, new C(0.78f, 0.62f, 0.52f), lined * 0.25f);
            C cloth = new C(s.ClothR, s.ClothG, s.ClothB), realm = new C(s.RealmR, s.RealmG, s.RealmB);
            bool beard = grownMan && h2 > 0.34f && s.Rank != 2 ? true : grownMan && h2 > 0.62f;
            bool coif = s.Woman && age >= 15f && h3 > 0.42f;
            bool bald = grownMan && age > 48f && h4 > 0.62f;
            C coifC = C.Lerp(new C(0.90f, 0.87f, 0.78f), cloth, 0.18f);
            C bgTop = new C(0.86f, 0.80f, 0.66f), bgFoot = new C(0.70f, 0.62f, 0.46f);

            // the frame's proportions
            float cx = 78f + (h5 - 0.5f) * 2f;
            float headR = 27f + child * 7f, headRy = headR * (1.20f - child * 0.08f);
            float headY = 78f + child * 14f;
            float neckY = headY + headRy * 0.82f;
            float shoulderY = neckY + 15f - child * 3f;
            float shoulderW = (s.Woman ? 52f : 60f) * (1f - child * 0.28f);
            if (s.Rank == 1) shoulderW += 6f;

            for (int j = 0; j < h; j++)
                for (int i = 0; i < w; i++)
                {
                    float x = (i + 0.5f) / U, y = (j + 0.5f) / U, nx = x - cx;
                    // the ground: an oval of warm paper, darker to its edge
                    float r = (float)Math.Sqrt((x - 78f) * (x - 78f) / (70f * 70f) + (y - 98f) * (y - 98f) / (92f * 92f));
                    C c = C.Lerp(bgTop, bgFoot, Clamp01(y / 196f));
                    c = c.Dark(Clamp01((r - 0.45f) * 0.55f));
                    // a soft halo behind a first
                    if (s.Rank == 1) c = C.Lerp(c, new C(0.96f, 0.84f, 0.48f), 0.55f * Clamp01(1f - Ell(x, y, cx, headY, headR * 1.65f, headRy * 1.55f) / 8f - 0.0f) * Sat(Ell(x, y, cx, headY, headR * 1.65f, headRy * 1.55f)));
                    // long hair behind a woman's or a girl's head
                    if ((s.Woman && !coif) || (s.Woman && age < 15f))
                    {
                        float d = Ell(x, y, cx, headY + 16f, headR + 7f, headRy + 22f);
                        c = C.Lerp(c, hair.Dark(0.12f), Sat(d));
                    }
                    // the body: a rounded shoulder line falling away
                    float slope = shoulderY + (nx * nx) / (shoulderW * shoulderW) * 34f;
                    float body = Math.Max(slope - y, Math.Abs(nx) - shoulderW);
                    if (body < 1.5f)
                    {
                        C top = cloth;
                        if (s.Rank == 2) top = new C(0.46f, 0.47f, 0.50f);   // (a sworn man's iron over his cloth)
                        // cloth's folds: shade down its sides
                        float side = Clamp01(Math.Abs(nx) / shoulderW);
                        C clo = top.Dark(0.08f + 0.34f * side * side);
                        clo = clo.Dark(Clamp01((y - shoulderY) / 110f) * 0.25f);
                        // folds: two soft darker lines
                        float fold = (float)Math.Sin(nx * 0.22f + h0 * 6f + (y - shoulderY) * 0.02f);
                        clo = clo.Dark(0.08f * Clamp01(fold * fold * fold));
                        if (s.Rank == 2)
                        {
                            // mail: small rows; the realm's colour at the shoulder
                            float mail = (float)Math.Sin(x * 1.3f) * (float)Math.Sin(y * 1.3f);
                            clo = clo.Dark(0.12f * Clamp01(mail));
                            if (Math.Abs(nx) > shoulderW * 0.62f) clo = C.Lerp(clo, realm, 0.82f);
                        }
                        c = C.Lerp(c, clo, Sat(body));
                        // the neck's opening and a band at it
                        float collar = Ell(x, y, cx, neckY + 6f, 15f - child * 3f, 13f);
                        if (s.Rank != 2) c = C.Lerp(c, cloth.Dark(0.5f), Sat(Math.Abs(collar) - 1.4f) * Sat(body));
                        // the first's mantle: the realm's colour from the shoulders, a gold clasp at the breast
                        if (s.Rank == 1)
                        {
                            float inner = (shoulderW * 0.40f) + (y - shoulderY) * 0.22f;
                            if (Math.Abs(nx) > inner) c = C.Lerp(c, realm.Dark(0.20f + 0.25f * side), 0.96f * Sat(body));
                            float clasp = Ell(x, y, cx, neckY + 22f, 5.2f, 5.2f);
                            c = C.Lerp(c, new C(0.97f, 0.79f, 0.32f), Sat(clasp));
                        }
                    }
                    // the neck
                    float neck = Box(x, y, cx - (11f - child * 2f), headY, cx + (11f - child * 2f), neckY + 8f);
                    c = C.Lerp(c, skin.Dark(0.16f), Sat(neck));
                    // the head: an egg, narrower at the jaw
                    float yy = y - headY, jaw = yy > 0f ? 1f - 0.20f * Clamp01(yy / headRy) * (1f - child * 0.6f) : 1f;
                    float head = Ell(nx, yy, 0f, 0f, headR * jaw, headRy);
                    C face = C.Lerp(skin, skin.Dark(0.20f), Clamp01((Math.Abs(nx) / headR - 0.5f) * 1.2f));
                    face = C.Lerp(face, new C(0.88f, 0.52f, 0.42f), 0.14f * Clamp01(1f - Ell(Math.Abs(nx), yy, headR * 0.48f, headRy * 0.25f, 11f, 8f) / 8f));   // (the cheeks)
                    // the lines of age: across the brow, beside the mouth
                    if (lined > 0f)
                    {
                        for (int k = 0; k < 2; k++)
                        {
                            float by = -headRy * (0.42f + 0.11f * k);
                            float l = Math.Abs(yy - by - (float)Math.Sin(nx * 0.12f) * 0.8f) - 0.55f;
                            if (Math.Abs(nx) < headR * 0.55f) face = C.Lerp(face, skin.Dark(0.34f), lined * 0.55f * Sat(l));
                        }
                        float crease = Math.Abs((Math.Abs(nx) - headR * 0.30f) - (yy - headRy * 0.30f) * 0.34f) - 0.5f;
                        if (yy > headRy * 0.18f && yy < headRy * 0.52f) face = C.Lerp(face, skin.Dark(0.32f), lined * 0.6f * Sat(crease));
                    }
                    // eyes, brows, a nose's shade, a mouth
                    float eyeY = -headRy * (0.05f - child * 0.08f), eyeX = headR * 0.40f;
                    float eye = Ell(Math.Abs(nx), yy, eyeX, eyeY, 3.9f + child * 0.8f, 2.6f + child * 0.5f);
                    face = C.Lerp(face, new C(0.97f, 0.95f, 0.90f), Sat(eye));
                    float pupil = Ell(Math.Abs(nx), yy, eyeX, eyeY, 2.1f + child * 0.5f, 2.3f + child * 0.4f);
                    face = C.Lerp(face, new C(0.14f, 0.10f, 0.07f), Sat(pupil));
                    float brow = Math.Abs(yy - (eyeY - 5.6f) - (Math.Abs(nx) - eyeX) * 0.10f) - 1.0f;
                    if (Math.Abs(Math.Abs(nx) - eyeX) < 6.2f) face = C.Lerp(face, hair.Dark(0.25f), Sat(brow));
                    if (Math.Abs(nx) < 2.0f && yy > eyeY && yy < headRy * 0.30f) face = C.Lerp(face, skin.Dark(0.20f), 0.55f * Clamp01(1f - Math.Abs(nx + 0.9f) / 1.6f));
                    float noseTip = Ell(nx, yy, 0f, headRy * 0.30f, 3.2f, 1.7f);
                    face = C.Lerp(face, skin.Dark(0.22f), 0.7f * Sat(noseTip));
                    float mouthY = headRy * (0.53f - child * 0.05f);
                    float mouth = Math.Max(Math.Abs(yy - mouthY) - 0.9f, Math.Abs(nx) - 7f);
                    face = C.Lerp(face, new C(0.52f, 0.22f, 0.17f), Sat(mouth));
                    c = C.Lerp(c, face, Sat(head));
                    // a beard: the jaw's lower part
                    if (beard)
                    {
                        float bh = 0.30f + h4 * 0.25f;
                        float be = Math.Max(head - 2f, headRy * bh - yy);
                        if (head < 2f) c = C.Lerp(c, hair.Dark(0.08f), Sat(be));
                        // a little below the jaw
                        float chin = Ell(nx, yy, 0f, headRy * 0.98f, headR * 0.48f, headRy * (0.14f + h4 * 0.22f));
                        c = C.Lerp(c, hair.Dark(0.1f), Sat(chin));
                        // the mouth, drawn in the beard
                        float inBeard = Math.Max(Math.Abs(yy - mouthY) - 1.1f, Math.Abs(nx) - 6f);
                        c = C.Lerp(c, new C(0.46f, 0.20f, 0.16f), Sat(inBeard));
                    }
                    // hair over the head's top, or a coif, or an iron cap
                    float topLine = -headRy * (0.28f + 0.14f * h2) + Math.Abs(nx) * Math.Abs(nx) * 0.012f;
                    if (s.Rank == 2)
                    {
                        float capD = Math.Max(Ell(nx, yy, 0f, -2f, headR + 3f, headRy + 1f), yy + headRy * 0.32f);
                        c = C.Lerp(c, new C(0.42f, 0.43f, 0.47f).Dark(Clamp01((nx + 12f) / 60f) * 0.45f), Sat(capD));
                        float noseGuard = Box(nx, yy, -2f, -headRy * 0.4f, 2f, headRy * 0.16f);
                        c = C.Lerp(c, new C(0.36f, 0.37f, 0.40f), Sat(noseGuard));
                        float brim = Math.Max(Math.Abs(yy + headRy * 0.34f) - 1.8f, Math.Abs(nx) - headR - 3f);
                        c = C.Lerp(c, realm.Dark(0.1f), Sat(brim));
                    }
                    else if (coif)
                    {
                        // a linen coif framing the face, down to the shoulders
                        float outer = Ell(nx, yy, 0f, 6f, headR + 7f, headRy + 13f);
                        float inner = Ell(nx, yy, 0f, 6f, headR * 0.72f, headRy * 0.78f);
                        float cf = Math.Max(outer, -inner);
                        C linen = coifC.Dark(Clamp01((Math.Abs(nx) / (headR + 7f) - 0.45f)) * 0.26f);
                        c = C.Lerp(c, linen, Sat(cf));
                    }
                    else
                    {
                        // hair: the crown; bald men keep only its sides
                        float top = Math.Max(Ell(nx, yy, 0f, -1f, headR + 2.4f, headRy + 2.4f), yy - topLine);
                        if (bald)
                        {
                            float crown = Ell(nx, yy, 0f, -headRy * 0.70f, headR * 0.82f, headRy * 0.40f);
                            top = Math.Max(top, -crown);
                        }
                        C hc = hair.Light(0.10f * Clamp01((-yy - headRy * 0.4f) / 20f));
                        // a few strands
                        hc = hc.Dark(0.10f * Clamp01((float)Math.Sin(nx * 0.75f + h0 * 9f) + 0.1f));
                        c = C.Lerp(c, hc, Sat(top));
                        if (s.Woman && !coif && age >= 6f)
                        {
                            // a parting at the middle
                            float part = Math.Abs(nx) - 0.7f;
                            if (yy < -headRy * 0.42f && top < 0f) c = C.Lerp(c, skin.Dark(0.1f), 0.55f * Sat(part));
                        }
                    }
                    // the first's circlet of gold
                    if (s.Rank == 1)
                    {
                        float cir = Math.Max(Math.Abs(yy + headRy * 0.46f + Math.Abs(nx) * Math.Abs(nx) * 0.006f) - 2.4f, Math.Abs(nx) - (headR + 2f));
                        c = C.Lerp(c, new C(0.97f, 0.80f, 0.34f).Dark(Clamp01(Math.Abs(nx) / headR) * 0.25f), Sat(cir));
                        for (int k = -1; k <= 1; k++)
                        {
                            float gem = Ell(nx, yy, k * headR * 0.48f, -headRy * 0.46f - 4f + Math.Abs(k) * 1.2f, 2.6f, 3.6f);
                            c = C.Lerp(c, new C(0.97f, 0.80f, 0.34f), Sat(gem));
                        }
                        float gem0 = Ell(nx, yy, 0f, -headRy * 0.46f, 1.8f, 1.8f);
                        c = C.Lerp(c, realm.Light(0.15f), Sat(gem0));
                    }
                    // the dead in faded tones
                    if (!s.Alive)
                    {
                        float g = c.R * 0.3f + c.G * 0.59f + c.B * 0.11f;
                        c = C.Lerp(c, new C(g * 0.98f, g * 0.95f, g * 0.88f), 0.78f);
                    }
                    // the oval's rim: a thin gold line, the corners clear for the page's own paper
                    float rim = Math.Abs(r - 0.965f) * 80f - 1.0f;
                    c = C.Lerp(c, new C(0.62f, 0.45f, 0.20f), Clamp01(0.5f - rim * 0.6f));
                    float alpha = Clamp01((1.0f - r) * 80f / 1.2f + 0.5f);
                    int o = (j * w + i) * 4;
                    rgba[o] = Clamp01(c.R); rgba[o + 1] = Clamp01(c.G); rgba[o + 2] = Clamp01(c.B); rgba[o + 3] = alpha;
                }
        }
    }
}

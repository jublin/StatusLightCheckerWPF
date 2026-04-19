namespace StatusLightChecker.Core.Colors;

public class StatusColors
{
    public ColorHex Available { get; set; } = new ColorHex(0x00FF00);
    public ColorHex Busy { get; set; } = new ColorHex(0xFF0000);
    public ColorHex DoNotDisturb { get; set; } = new ColorHex(0x8B0000);
    public ColorHex Away { get; set; } = new ColorHex(0xFFFF00);
    public ColorHex Offline { get; set; } = new ColorHex(0x000000);
    public ColorHex OutOfOffice { get; set; } = new ColorHex(0x800080);
    public ColorHex Unknown { get; set; } = new ColorHex(0x808080);
}

public class ColorHex
{
    public byte R { get; set; }
    public byte G { get; set; }
    public byte B { get; set; }

    public ColorHex() { }

    public ColorHex(uint rgb)
    {
        R = (byte)(rgb >> 16);
        G = (byte)(rgb >> 8);
        B = (byte)rgb;
    }

    public ColorHex(byte r, byte g, byte b)
    {
        R = r;
        G = g;
        B = b;
    }

    public override string ToString() => $"#{R:X2}{G:X2}{B:X2}";
}

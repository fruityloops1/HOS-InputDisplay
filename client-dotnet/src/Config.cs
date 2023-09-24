using IniParser;
using IniParser.Model;
using Raylib_cs;

namespace InputDisplay;

class Config
{
    private static Color HexToColor(string hex)
    {
        hex = hex.TrimStart('#');
        int hexValue = Convert.ToInt32(hex, 16);

        return new Color((byte)((hexValue & -16777216) >> 0x18),
                              (byte)((hexValue & 0xff0000) >> 0x10),
                              (byte)((hexValue & 0xff00) >> 8),
                              (byte)(hexValue & 0xff));
    }

    public Config(string path)
    {
        var parser = new FileIniDataParser();
        IniData data = parser.ReadFile(path);

        try
        {
            PacketsPerSecond = Convert.ToInt32(data["config"]["packetsPerSecond"]);
            PacketOnVsyncEvent = false;
        }
        catch (FormatException)
        {
            PacketOnVsyncEvent = data["config"]["packetsPerSecond"] == "vsync";
        }
        Host = data["config"]["host"];
        FontPath = data["config"]["fontPath"];
        UseSystemButtonColor = data["config"]["useSystemButtonColor"] == "true";
        UseSystemControllerColor = data["config"]["useSystemControllerColor"] == "true";
        EnableGyroModels = data["config"]["enableGyroModels"] == "true";

        ActiveColor = HexToColor(data["colors"]["active"]);
        InactiveColor = HexToColor(data["colors"]["inactive"]);
        StickColor = HexToColor(data["colors"]["stick"]);
        FontColor = HexToColor(data["colors"]["font"]);
        BackgroundColor = HexToColor(data["colors"]["background"]);
        ControllerColor = HexToColor(data["colors"]["controller"]);
    }

    public int PacketsPerSecond;
    public bool PacketOnVsyncEvent;
    public string Host;
    public string FontPath;

    public Color ActiveColor;
    public Color InactiveColor;
    public Color StickColor;
    public Color FontColor;
    public Color BackgroundColor;
    public Color ControllerColor;
    public bool UseSystemButtonColor;
    public bool UseSystemControllerColor;
    public bool EnableGyroModels;
}
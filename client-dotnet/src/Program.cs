using Raylib_cs;
using System.Net;
using System.Net.Sockets;
using System.Numerics;
using System;

namespace InputDisplay;

class Program
{
    private static Config config = new Config("Res/config.ini");
    private static Font buttonFont;
    private static Camera3D camera;
    private static Model controllerModel;

    public static UInt64 Keys;
    public static Vector2 LeftStick, RightStick;

    private static void DrawButton(Vector2 pos, float width, float height, HidNpadButton button, string label, float roundness, float fontOffsetX, float fontOffsetY, float fontSize, int materialIndex)
    {
        Rectangle rec = new Rectangle(pos.X - width, pos.Y - height, width * 2, height * 2);
        Raylib.DrawRectangleRounded(rec, roundness, 8, (Keys & Convert.ToUInt64(button)) != 0 ? config.ActiveColor : config.InactiveColor);
        pos.X += fontOffsetX;
        pos.Y += fontOffsetY;
        Raylib.DrawTextEx(buttonFont, label, pos, fontSize, 4, config.FontColor);

        unsafe
        {
            if (materialIndex != -1)
                controllerModel.materials[materialIndex].maps[0].color = (Keys & Convert.ToUInt64(button)) != 0 ? config.ActiveColor : config.InactiveColor;
        }
    }

    private static void DrawStick(Vector2 pos, Vector2 stick, HidNpadButton button, int materialIndex)
    {
        Vector2 at = new Vector2(pos.X + stick.X / 800, pos.Y + -stick.Y / 800);
        Raylib.DrawRing(pos, 56, 50, 0, 360, 40, config.InactiveColor);
        Raylib.DrawCircleV(at, 40, (Keys & Convert.ToUInt64(button)) != 0 ? config.ActiveColor : config.StickColor);

        unsafe
        {
            if (materialIndex != -1)
                controllerModel.materials[materialIndex].maps[0].color = (Keys & Convert.ToUInt64(button)) != 0 ? config.ActiveColor : config.StickColor;
        }
    }

    public static void Main()
    {
        Raylib.InitWindow(619, 360, "HOS-InputDisplay");

        buttonFont = Raylib.LoadFontEx(config.FontPath, 38, null, 0);
        camera.position = new Vector3(0, -15, 15);
        camera.target = new Vector3(0, 0, -6);
        camera.up = new Vector3(0, 1, 0);
        camera.fovy = 45;
        camera.projection = CameraProjection.CAMERA_PERSPECTIVE;
        controllerModel = Raylib.LoadModel("Res/Vallarta_Christopher_HW2_Controller.obj");

        UdpClient client = new UdpClient(config.Host, 44302);
        IPEndPoint remote = null;

        byte[] sendData = new byte[] { 0, 0, 0, 0 };
        client.Send(sendData, sendData.Length);
        sendData = new byte[] { 0, 0, 0, 0, (byte)config.PacketsPerSecond, 0, 0, 0 };
        client.Send(sendData, sendData.Length);

        Quaternion slerpTo = new Quaternion(0, 0, 0, 1);
        Quaternion baseRotation = new Quaternion(0, 0, 0, 1);
        int calibrationCooldown = -1;


        unsafe
        {
            controllerModel.materials[2].maps[0].color = config.ControllerColor;
            controllerModel.materials[3].maps[0].color = config.InactiveColor;
        }

        while (true)
        {
            byte[] data = client.Receive(ref remote);
            Keys = BitConverter.ToUInt64(data, 0);
            LeftStick = new Vector2((float)BitConverter.ToInt32(data, 8), (float)BitConverter.ToInt32(data, 12));
            RightStick = new Vector2((float)BitConverter.ToInt32(data, 16), (float)BitConverter.ToInt32(data, 20));

            float[,] directionMatrix = {
                {BitConverter.ToSingle(data, 76), BitConverter.ToSingle(data, 80), BitConverter.ToSingle(data, 84)},
                {BitConverter.ToSingle(data, 88), BitConverter.ToSingle(data, 92), BitConverter.ToSingle(data, 96)},
                {BitConverter.ToSingle(data, 100), BitConverter.ToSingle(data, 104), BitConverter.ToSingle(data, 108)}
            };
            Quaternion quat = MathUtil.ToQuaternion(directionMatrix);
            quat.X *= -1;
            quat.Y *= -1;
            quat.Z *= -1;
            controllerModel.transform = Raymath.QuaternionToMatrix(Raymath.QuaternionMultiply(Raymath.QuaternionInvert(baseRotation), quat));

            if (calibrationCooldown > 0)
            {
                calibrationCooldown--;

                baseRotation = Raymath.QuaternionSlerp(baseRotation, slerpTo, ((float)config.PacketsPerSecond / 2 - (float)calibrationCooldown) / (float)config.PacketsPerSecond / 2);
            }
            if ((Keys & Convert.ToUInt64(HidNpadButton.StickL)) != 0 && calibrationCooldown <= 0)
            {
                calibrationCooldown = config.PacketsPerSecond / 2;
                slerpTo = MathUtil.ToQuaternion(directionMatrix);
                slerpTo.X *= -1;
                slerpTo.Y *= -1;
                slerpTo.Z *= -1;
            }

            Raylib.BeginDrawing();
            Raylib.ClearBackground(config.BackgroundColor);

            Raylib.BeginMode3D(camera);

            Raylib.DrawModel(controllerModel, new Vector3(0, 0, 0), 2, Color.WHITE);

            Raylib.EndMode3D();

            DrawStick(new Vector2(100, 180), LeftStick, HidNpadButton.StickL, 12);
            DrawStick(new Vector2(380, 270), RightStick, HidNpadButton.StickR, 13);

            DrawButton(new Vector2(550, 180), 25, 25, HidNpadButton.A, "A", 1, -12, -19, 38, 0);
            DrawButton(new Vector2(450, 180), 25, 25, HidNpadButton.Y, "Y", 1, -12, -19, 38, 15);
            DrawButton(new Vector2(500, 230), 25, 25, HidNpadButton.B, "B", 1, -12, -19, 38, 1);
            DrawButton(new Vector2(500, 130), 25, 25, HidNpadButton.X, "X", 1, -12, -19, 38, 14);
            DrawButton(new Vector2(380, 130), 12, 12, HidNpadButton.Plus, "+", 1, -9, -19, 38, 10);
            DrawButton(new Vector2(220, 130), 12, 12, HidNpadButton.Minus, "-", 1, -7, -21, 38, 9);
            DrawButton(new Vector2(500, 70), 40, 15, HidNpadButton.R, "R", 1, -12, -20, 38, 11);
            DrawButton(new Vector2(500, 30), 40, 15, HidNpadButton.ZR, "ZR", 1, -22, -19, 38, 17);
            DrawButton(new Vector2(100, 70), 40, 15, HidNpadButton.L, "L", 1, -12, -20, 38, 8);
            DrawButton(new Vector2(100, 30), 40, 15, HidNpadButton.ZL, "ZL", 1, -22, -19, 38, 16);
            DrawButton(new Vector2(220, 240), 15, 15, HidNpadButton.Up, "", 0.5f, 0, 0, 38, 7);
            DrawButton(new Vector2(220, 300), 15, 15, HidNpadButton.Down, "", 0.5f, 0, 0, 38, 4);
            DrawButton(new Vector2(190, 270), 15, 15, HidNpadButton.Left, "", 0.5f, 0, 0, 38, 5);
            DrawButton(new Vector2(250, 270), 15, 15, HidNpadButton.Right, "", 0.5f, 0, 0, 38, 6);
            Raylib.DrawRectangleRounded(new Rectangle(205, 250, 15 * 2, 20 * 2), 0, 8, config.InactiveColor);
            Raylib.DrawRectangleRounded(new Rectangle(200, 255, 20 * 2, 15 * 2), 0, 8, config.InactiveColor);

            if (calibrationCooldown > 0)
            {
                Raylib.DrawTextEx(buttonFont, "Recalibrating", new Vector2(180, 10), 38, 4, Color.WHITE);
            }

            Raylib.EndDrawing();
        }

        Raylib.CloseWindow();
    }
}
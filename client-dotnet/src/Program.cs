using Raylib_cs;
using System.Net;
using System.Net.Sockets;
using System.Numerics;
using System;

namespace InputDisplay;

class Program
{
    private static Config config = new Config("Res/config.ini");
    private static Font buttonFont, fatFont;
    private static Camera3D camera;
    private static Model proControllerModel, joyLeftModel, joyRightModel;

    public static UInt64 Keys;
    public static Vector2 LeftStick, RightStick;
    public static Color buttonColor, controllerColorLeft, controllerColorRight;

    private static void DrawButton(Vector2 pos, float width, float height, HidNpadButton button, string label, float roundness, float fontOffsetX, float fontOffsetY, float fontSize, int materialIndex, int materialIndexJoyL, int materialIndexJoyR)
    {
        Rectangle rec = new Rectangle(pos.X - width, pos.Y - height, width * 2, height * 2);
        Raylib.DrawRectangleRounded(rec, roundness, 8, (Keys & Convert.ToUInt64(button)) != 0 ? config.ActiveColor : config.InactiveColor);
        pos.X += fontOffsetX;
        pos.Y += fontOffsetY;
        Raylib.DrawTextEx(buttonFont, label, pos, fontSize, 4, config.UseSystemButtonColor ? GetTextColor(buttonColor) : config.FontColor);

        unsafe
        {
            if (materialIndex != -1)
                proControllerModel.materials[materialIndex].maps[0].color = (Keys & Convert.ToUInt64(button)) != 0 ? config.ActiveColor : config.InactiveColor;
            if (materialIndexJoyL != -1)
                joyLeftModel.materials[materialIndexJoyL].maps[0].color = (Keys & Convert.ToUInt64(button)) != 0 ? config.ActiveColor : config.InactiveColor;
            if (materialIndexJoyR != -1)
                joyRightModel.materials[materialIndexJoyR].maps[0].color = (Keys & Convert.ToUInt64(button)) != 0 ? config.ActiveColor : config.InactiveColor;
        }
    }

    private static void DrawStick(Vector2 pos, Vector2 stick, HidNpadButton button, int materialIndex, int materialIndexJoyL, int materialIndexJoyR)
    {
        Vector2 at = new Vector2(pos.X + stick.X / 800, pos.Y + -stick.Y / 800);
        Raylib.DrawRing(pos, 56, 50, 0, 360, 40, config.InactiveColor);
        Raylib.DrawCircleV(at, 40, (Keys & Convert.ToUInt64(button)) != 0 ? config.ActiveColor : config.StickColor);

        unsafe
        {
            if (materialIndex != -1)
                proControllerModel.materials[materialIndex].maps[0].color = (Keys & Convert.ToUInt64(button)) != 0 ? config.ActiveColor : config.StickColor;
            if (materialIndexJoyL != -1)
                joyLeftModel.materials[materialIndexJoyL].maps[0].color = (Keys & Convert.ToUInt64(button)) != 0 ? config.ActiveColor : config.UseSystemButtonColor ? config.InactiveColor : config.StickColor;
            if (materialIndexJoyR != -1)
                joyRightModel.materials[materialIndexJoyR].maps[0].color = (Keys & Convert.ToUInt64(button)) != 0 ? config.ActiveColor : config.UseSystemButtonColor ? config.InactiveColor : config.StickColor;
        }
    }

    private static float[,] GetDirectionMatrix(byte[] data, int index)
    {
        int start = 24 + 96 * index + 52;
        float[,] directionMatrix = {
                {BitConverter.ToSingle(data, start), BitConverter.ToSingle(data, start+4), BitConverter.ToSingle(data, start+8)},
                {BitConverter.ToSingle(data, start+12), BitConverter.ToSingle(data, start+16), BitConverter.ToSingle(data, start+20)},
                {BitConverter.ToSingle(data, start+24), BitConverter.ToSingle(data, start+28), BitConverter.ToSingle(data, start+32)}
            };
        return directionMatrix;
    }

    private static Quaternion GetControllerQuat(byte[] data, int index)
    {
        float[,] directionMatrix = GetDirectionMatrix(data, index);
        Quaternion quat = MathUtil.ToQuaternion(directionMatrix);
        quat.X *= -1;
        quat.Y *= -1;
        quat.Z *= -1;
        return quat;
    }

    private static Color GetTextColor(Color backgroundColor)
    {
        float luminosity = (backgroundColor.r * 0.299f + backgroundColor.g * 0.587f + backgroundColor.b * 0.114f) / 255f;
        return luminosity > 0.5f ? Color.BLACK : Color.WHITE;
    }

    public static Color GetStickColor(Color buttonColor)
    {
        float luminosity = (buttonColor.r * 0.299f + buttonColor.g * 0.587f + buttonColor.b * 0.114f) / 255f;
        int adjustFactor = luminosity > 0.5f ? -65 : 65;

        buttonColor.r = (byte)Math.Clamp(buttonColor.r + adjustFactor, 0, 255);
        buttonColor.g = (byte)Math.Clamp(buttonColor.g + adjustFactor, 0, 255);
        buttonColor.b = (byte)Math.Clamp(buttonColor.b + adjustFactor, 0, 255);
        return buttonColor;
    }

    public static void Main()
    {
        Raylib.InitWindow(619, 360, "HOS-InputDisplay");

        buttonFont = Raylib.LoadFontEx(config.FontPath, 38, null, 0);
        fatFont = Raylib.LoadFontEx("Res/Montserrat-Black.ttf", 38, null, 0);
        camera.position = new Vector3(0, -15, 15);
        camera.target = new Vector3(0, 0, -6);
        camera.up = new Vector3(0, 1, 0);
        camera.fovy = 45;
        camera.projection = CameraProjection.CAMERA_PERSPECTIVE;
        proControllerModel = Raylib.LoadModel("Res/Controller.glb");
        joyLeftModel = Raylib.LoadModel("Res/JoyL.glb");
        joyRightModel = Raylib.LoadModel("Res/JoyR.glb");

        UdpClient client = new UdpClient(config.Host, 44302);
        IPEndPoint remote = null;

        byte[] sendData = new byte[] { 0, 0, 0, 0 };
        client.Send(sendData, sendData.Length);
        sendData = new byte[] { 0, 0, 0, 0, (byte)config.PacketsPerSecond, 0, 0, 0 };
        client.Send(sendData, sendData.Length);

        Quaternion slerpTo = new Quaternion(0, 0, 0, 1);
        Quaternion baseRotation = new Quaternion(0, 0, 0, 1);
        Quaternion slerpTo2 = new Quaternion(0, 0, 0, 1);
        Quaternion baseRotation2 = new Quaternion(0, 0, 0, 1);
        int calibrationCooldown = -1;


        unsafe
        {
            proControllerModel.materials[2].maps[0].color = config.ControllerColor;
            proControllerModel.materials[7].maps[0].color = config.InactiveColor;
        }

        while (true)
        {
            byte[] data = client.Receive(ref remote);
            Keys = BitConverter.ToUInt64(data, 0);
            LeftStick = new Vector2((float)BitConverter.ToInt32(data, 8), (float)BitConverter.ToInt32(data, 12));
            RightStick = new Vector2((float)BitConverter.ToInt32(data, 16), (float)BitConverter.ToInt32(data, 20));
            int styleSet = BitConverter.ToInt32(data, 232);
            int colorButton = BitConverter.ToInt32(data, 220);
            int leftColor = BitConverter.ToInt32(data, 216);
            int rightColor = BitConverter.ToInt32(data, 224);

            controllerColorLeft = new Color((byte)(leftColor & 0xff),
                              (byte)((leftColor & 0xff00) >> 8),
                              (byte)((leftColor & 0xff0000) >> 0x10),
                               (byte)((leftColor & -16777216) >> 0x18)
                              );
            controllerColorRight = new Color((byte)(rightColor & 0xff),
                              (byte)((rightColor & 0xff00) >> 8),
                              (byte)((rightColor & 0xff0000) >> 0x10),
                               (byte)((rightColor & -16777216) >> 0x18)
                              );

            if (config.UseSystemButtonColor)
            {
                buttonColor = new Color((byte)(colorButton & 0xff),
                                  (byte)((colorButton & 0xff00) >> 8),
                                  (byte)((colorButton & 0xff0000) >> 0x10),
                                   (byte)((colorButton & -16777216) >> 0x18)
                                  );
                config.InactiveColor = buttonColor;
                config.StickColor = GetStickColor(buttonColor);
            }


            Raylib.BeginDrawing();
            Raylib.ClearBackground(config.BackgroundColor);


            if ((styleSet & Convert.ToInt32(HidNpadStyleTag.NpadJoyDual)) != 0 && config.EnableGyroModels)
            {
                unsafe
                {
                    if (config.UseSystemControllerColor)
                    {
                        joyLeftModel.materials[1].maps[0].color = controllerColorLeft;
                        joyLeftModel.materials[5].maps[0].color = controllerColorLeft;
                        joyLeftModel.materials[6].maps[0].color = controllerColorLeft;
                        joyRightModel.materials[7].maps[0].color = controllerColorRight;
                        joyRightModel.materials[8].maps[0].color = controllerColorRight;
                        joyRightModel.materials[9].maps[0].color = controllerColorRight;
                    }
                    joyLeftModel.materials[2].maps[0].color = config.InactiveColor;
                    joyRightModel.materials[1].maps[0].color = config.InactiveColor;
                }


                if (calibrationCooldown > 0)
                {
                    calibrationCooldown--;

                    baseRotation = Raymath.QuaternionSlerp(baseRotation, slerpTo, ((float)config.PacketsPerSecond / 2 - (float)calibrationCooldown) / (float)config.PacketsPerSecond / 2);
                    baseRotation2 = Raymath.QuaternionSlerp(baseRotation2, slerpTo2, ((float)config.PacketsPerSecond / 2 - (float)calibrationCooldown) / (float)config.PacketsPerSecond / 2);
                }
                if ((Keys & Convert.ToUInt64(HidNpadButton.StickL)) != 0 && calibrationCooldown <= 0)
                {
                    calibrationCooldown = config.PacketsPerSecond / 2;
                    slerpTo = GetControllerQuat(data, 0);
                    slerpTo2 = GetControllerQuat(data, 1);
                }

                Quaternion leftQuat = GetControllerQuat(data, 0);
                joyLeftModel.transform = Raymath.QuaternionToMatrix(Raymath.QuaternionMultiply(Raymath.QuaternionInvert(baseRotation), leftQuat));
                Quaternion rightQuat = GetControllerQuat(data, 1);
                joyRightModel.transform = Raymath.QuaternionToMatrix(Raymath.QuaternionMultiply(Raymath.QuaternionInvert(baseRotation2), rightQuat));

                Raylib.BeginMode3D(camera);

                Raylib.DrawModel(joyLeftModel, new Vector3(-4, 0, 0), 2, Color.WHITE);
                Raylib.DrawModel(joyRightModel, new Vector3(4, 0, 0), 2, Color.WHITE);

                Raylib.EndMode3D();
            }
            else if ((styleSet & Convert.ToInt32(HidNpadStyleTag.NpadFullKey)) != 0 && config.EnableGyroModels)
            {
                unsafe
                {
                    if (config.UseSystemControllerColor)
                        proControllerModel.materials[2].maps[0].color = controllerColorLeft;
                    proControllerModel.materials[7].maps[0].color = config.InactiveColor;
                }

                Quaternion quat = GetControllerQuat(data, 0);
                proControllerModel.transform = Raymath.QuaternionToMatrix(Raymath.QuaternionMultiply(Raymath.QuaternionInvert(baseRotation), quat));

                if (calibrationCooldown > 0)
                {
                    calibrationCooldown--;

                    baseRotation = Raymath.QuaternionSlerp(baseRotation, slerpTo, ((float)config.PacketsPerSecond / 2 - (float)calibrationCooldown) / (float)config.PacketsPerSecond / 2);
                }
                if ((Keys & Convert.ToUInt64(HidNpadButton.StickL)) != 0 && calibrationCooldown <= 0)
                {
                    calibrationCooldown = config.PacketsPerSecond / 2;
                    slerpTo = GetControllerQuat(data, 0);
                }


                Raylib.BeginMode3D(camera);

                Raylib.DrawModel(proControllerModel, new Vector3(0, 0, 0), 2, Color.WHITE);

                Raylib.EndMode3D();
            }

            DrawStick(new Vector2(100, 180), LeftStick, HidNpadButton.StickL, 8, 7, -1);
            DrawStick(new Vector2(380, 270), RightStick, HidNpadButton.StickR, 9, -1, 10);

            DrawButton(new Vector2(550, 180), 25, 25, HidNpadButton.A, "A", 1, -12, -19, 38, 10, -1, 3);
            DrawButton(new Vector2(450, 180), 25, 25, HidNpadButton.Y, "Y", 1, -12, -19, 38, 13, -1, 4);
            DrawButton(new Vector2(500, 230), 25, 25, HidNpadButton.B, "B", 1, -12, -19, 38, 12, -1, 2);
            DrawButton(new Vector2(500, 130), 25, 25, HidNpadButton.X, "X", 1, -12, -19, 38, 11, -1, 5);
            DrawButton(new Vector2(380, 130), 12, 12, HidNpadButton.Plus, "+", 1, -9, -19, 38, 14, -1, 12);
            DrawButton(new Vector2(220, 130), 12, 12, HidNpadButton.Minus, "-", 1, -7, -21, 38, 15, 3, -1);
            DrawButton(new Vector2(500, 70), 40, 15, HidNpadButton.R, "R", 1, -12, -20, 38, 18, -1, 6);
            DrawButton(new Vector2(500, 30), 40, 15, HidNpadButton.ZR, "ZR", 1, -22, -19, 38, 16, -1, 11);
            DrawButton(new Vector2(100, 70), 40, 15, HidNpadButton.L, "L", 1, -12, -20, 38, 17, 12, -1);
            DrawButton(new Vector2(100, 30), 40, 15, HidNpadButton.ZL, "ZL", 1, -22, -19, 38, 1, 11, -1);
            DrawButton(new Vector2(220, 240), 15, 15, HidNpadButton.Up, "", 0.5f, 0, 0, 38, 3, 8, -1);
            DrawButton(new Vector2(220, 300), 15, 15, HidNpadButton.Down, "", 0.5f, 0, 0, 38, 5, 10, -1);
            DrawButton(new Vector2(190, 270), 15, 15, HidNpadButton.Left, "", 0.5f, 0, 0, 38, 6, 4, -1);
            DrawButton(new Vector2(250, 270), 15, 15, HidNpadButton.Right, "", 0.5f, 0, 0, 38, 4, 9, -1);
            Raylib.DrawRectangleRounded(new Rectangle(205, 250, 15 * 2, 20 * 2), 0, 8, config.InactiveColor);
            Raylib.DrawRectangleRounded(new Rectangle(200, 255, 20 * 2, 15 * 2), 0, 8, config.InactiveColor);

            if (calibrationCooldown > 0)
            {
                Raylib.DrawTextEx(fatFont, "Recalibrating", new Vector2(180, 10), 38, 2, Color.WHITE);
            }

            Raylib.EndDrawing();
        }

        Raylib.CloseWindow();
    }
}
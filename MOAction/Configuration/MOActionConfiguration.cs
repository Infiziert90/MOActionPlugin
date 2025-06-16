using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Configuration;
using Dalamud.Interface.Colors;

namespace MOAction.Configuration;

[Serializable]
public class MOActionConfiguration : IPluginConfiguration
{
    public int Version { get; set; } = 6;
    public List<ConfigurationEntry> Stacks { get; private set; }

    public int CrosshairWidth;
    public int CrosshairHeight;
    public bool DrawCrosshair = false;
    public float CrosshairThickness = 5.0f;
    public float CrosshairSize = 15.0f;
    public Vector4 CrosshairInvalidColor = ImGuiColors.DalamudRed;
    public Vector4 CrosshairValidColor = ImGuiColors.DalamudOrange;
    public Vector4 CrosshairCastColor = ImGuiColors.ParsedGreen;

    public bool RangeCheck;

    public MOActionConfiguration()
    {
        Stacks = [];
        RangeCheck = false;
        InitializeCrosshairLocation();
    }

    private unsafe void InitializeCrosshairLocation(){
        var dev = FFXIVClientStructs.FFXIV.Client.Graphics.Kernel.Device.Instance();
        CrosshairWidth = (int)dev->Width/2;
        CrosshairHeight = (int)dev->Height/2;
    }
}
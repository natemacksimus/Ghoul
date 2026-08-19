using System.IO;
using UnityEngine;

/// <summary>
/// One frame of player input packed as a button bitmask + two analog axes.
/// 10 bytes on the wire. Unmanaged so NGO can write/read it with WriteValueSafe.
///
/// All button bits represent EVENTS (rising edge this frame), not held state,
/// except Move/Aim which are instantaneous analog samples.
/// InputCapture is responsible for detecting rising edges before encoding.
/// </summary>
public struct RollbackInput
{
    public ushort Buttons;
    // Analog axes encoded as short (÷32767f to recover [-1,1])
    public short MoveX;
    public short MoveY;
    public short AimX;
    public short AimY;

    // ── Button bit constants ──────────────────────────────────────────────
    public const ushort BtnJump        = 1 << 0;
    public const ushort BtnJumpRelease = 1 << 1;
    public const ushort BtnUseRight    = 1 << 2;
    public const ushort BtnUseLeft     = 1 << 3;
    public const ushort BtnDodge       = 1 << 4;
    public const ushort BtnInvLeft     = 1 << 5;
    public const ushort BtnInvRight    = 1 << 6;
    public const ushort BtnInteract    = 1 << 7;
    public const ushort BtnPickup      = 1 << 8;
    public const ushort BtnDropLeft    = 1 << 9;
    public const ushort BtnDropRight   = 1 << 10;

    public bool IsPressed(ushort btn) => (Buttons & btn) != 0;

    public Vector2 MoveVector => new Vector2(MoveX / 32767f, MoveY / 32767f);
    public Vector2 AimVector  => new Vector2(AimX  / 32767f, AimY  / 32767f);

    // ── Encoding helpers ──────────────────────────────────────────────────
    public static short EncodeAxis(float v) => (short)(Mathf.Clamp(v, -1f, 1f) * 32767f);

    // ── Serialization (10 bytes) ──────────────────────────────────────────
    public void Serialize(BinaryWriter w)
    {
        w.Write(Buttons);
        w.Write(MoveX); w.Write(MoveY);
        w.Write(AimX);  w.Write(AimY);
    }

    public static RollbackInput Deserialize(BinaryReader r) => new RollbackInput
    {
        Buttons = r.ReadUInt16(),
        MoveX   = r.ReadInt16(), MoveY = r.ReadInt16(),
        AimX    = r.ReadInt16(), AimY  = r.ReadInt16(),
    };

    public bool Equals(RollbackInput other) =>
        Buttons == other.Buttons &&
        MoveX == other.MoveX && MoveY == other.MoveY &&
        AimX  == other.AimX  && AimY  == other.AimY;
}

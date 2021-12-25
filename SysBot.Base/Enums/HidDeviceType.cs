namespace SysBot.Base
{
    /// <summary>
    /// Device Type
    /// </summary>
    public enum HidDeviceType
    {
        JoyRight1 = 1,   ///< ::HidDeviceTypeBits_JoyRight
        JoyLeft2 = 2,   ///< ::HidDeviceTypeBits_JoyLeft
        FullKey3 = 3,   ///< ::HidDeviceTypeBits_FullKey
        JoyLeft4 = 4,   ///< ::HidDeviceTypeBits_JoyLeft
        JoyRight5 = 5,   ///< ::HidDeviceTypeBits_JoyRight
        FullKey6 = 6,   ///< ::HidDeviceTypeBits_FullKey
        LarkHvcLeft = 7,   ///< ::HidDeviceTypeBits_LarkHvcLeft, ::HidDeviceTypeBits_HandheldLarkHvcLeft
        LarkHvcRight = 8,   ///< ::HidDeviceTypeBits_LarkHvcRight, ::HidDeviceTypeBits_HandheldLarkHvcRight
        LarkNesLeft = 9,   ///< ::HidDeviceTypeBits_LarkNesLeft, ::HidDeviceTypeBits_HandheldLarkNesLeft
        LarkNesRight = 10,  ///< ::HidDeviceTypeBits_LarkNesRight, ::HidDeviceTypeBits_HandheldLarkNesRight
        Lucia = 11,  ///< ::HidDeviceTypeBits_Lucia
        Palma = 12,  ///< [9.0.0+] ::HidDeviceTypeBits_Palma
        FullKey13 = 13,  ///< ::HidDeviceTypeBits_FullKey
        FullKey15 = 15,  ///< ::HidDeviceTypeBits_FullKey
        DebugPad = 17,  ///< ::HidDeviceTypeBits_DebugPad
        System19 = 19,  ///< ::HidDeviceTypeBits_System with \ref HidNpadStyleTag |= ::HidNpadStyleTag_NpadFullKey.
        System20 = 20,  ///< ::HidDeviceTypeBits_System with \ref HidNpadStyleTag |= ::HidNpadStyleTag_NpadJoyDual.
        System21 = 21,  ///< ::HidDeviceTypeBits_System with \ref HidNpadStyleTag |= ::HidNpadStyleTag_NpadJoyDual.
        Lagon = 22,  ///< ::HidDeviceTypeBits_Lagon
        Lager = 28,  ///< ::HidDeviceTypeBits_Lager
    }
}
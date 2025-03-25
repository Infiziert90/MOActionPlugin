using Dalamud.Game;

namespace MOAction;

public class MOActionAddressResolver
{
    public nint GtQueuePatch { get; private set; }
    public byte[] PreGtQueuePatchData { get; set; }

    public MOActionAddressResolver(ISigScanner sig)
    {
        //7.2
        //this specific adress is inside the useAction function, in there exists a switch statement, 
        //on abilities (4u) there's a small note to set a boolean depending on if it's a ground target spell or not
        //case 4u:
        //   v24 = v23 ^ 1;
        //   break;
        //this signature refers to the XOR operator in said check
        GtQueuePatch = sig.ScanModule("0F B6 C2 34 01 84 C0 74 8C");
    }
}
using StageLink.Protocol;

namespace StageLink.Web.Speakers;

// The speaker answered a command with Nack.
public class SpeakerCommandException : Exception
{
    public SpeakerCommandException(Command command, NackCode code)
        : base($"Speaker rejected {command}: {Describe(code)}.")
    {
        Command = command;
        Code = code;
    }

    public Command Command { get; }

    public NackCode Code { get; }

    private static string Describe(NackCode code)
    {
        return code switch
        {
            NackCode.UnsupportedCommand => "the speaker does not support this command",
            NackCode.InvalidPayload => "the request data was invalid",
            NackCode.ValueOutOfRange => "the value is out of range",
            _ => $"error code 0x{(byte)code:X2}",
        };
    }
}

using StageLink.Protocol;

namespace StageLink.Simulator;

public class Speaker
{
    private readonly SpeakerState _state;

    public Speaker(SpeakerState state)
    {
        _state = state;
    }

    public Frame Handle(Frame request)
    {
        return request.Command switch
        {
            Command.Ping => Reply(request, Command.Pong, []),
            _ => Nack(request, NackCode.UnsupportedCommand),
        };
    }

    private static Frame Reply(Frame request, Command command, byte[] payload)
    {
        return new Frame(command, request.MessageNumber, payload);
    }

    private static Frame Nack(Frame request, NackCode code)
    {
        return Reply(request, Command.Nack, [(byte)code]);
    }
}
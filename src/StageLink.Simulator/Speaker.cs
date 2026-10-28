using StageLink.Protocol;

namespace StageLink.Simulator;

public class Speaker
{
    private const int FixedPointSize = 2;

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
            Command.GetState => HandleGetState(request),
            Command.SetGain => HandleFixedPointSetting(request, _state.SetGain),
            Command.SetMute => HandleSetMute(request),
            Command.SetTilt => HandleFixedPointSetting(request, _state.SetTilt),
            _ => Nack(request, NackCode.UnsupportedCommand),
        };
    }

    // State data: name, gain (2), mute (1), tilt (2)
    private Frame HandleGetState(Frame request)
    {
        if (request.Payload.Length != 0)
        {
            return Nack(request, NackCode.InvalidPayload);
        }

        var writer = new ByteWriter();
        writer.WriteString(_state.Name);
        writer.WriteFixedPoint(_state.Gain);
        writer.WriteByte((byte)(_state.Muted ? 1 : 0));
        writer.WriteFixedPoint(_state.Tilt);

        return Reply(request, Command.State, writer.ToArray());
    }

    // Shared by SetGain and SetTilt: both carry one fixed-point value.
    private static Frame HandleFixedPointSetting(Frame request, Action<double> apply)
    {
        byte[] payload = request.Payload;
        if (payload.Length != FixedPointSize)
        {
            return Nack(request, NackCode.InvalidPayload);
        }

        double value = new ByteReader(payload).ReadFixedPoint();

        try
        {
            apply(value);
        }
        catch (ArgumentOutOfRangeException)
        {
            return Nack(request, NackCode.ValueOutOfRange);
        }

        return Ack(request);
    }

    private Frame HandleSetMute(Frame request)
    {
        byte[] payload = request.Payload;
        if (payload.Length != 1 || payload[0] > 1)
        {
            return Nack(request, NackCode.InvalidPayload);
        }

        _state.SetMute(payload[0] == 1);
        return Ack(request);
    }

    private static Frame Reply(Frame request, Command command, byte[] payload)
    {
        return new Frame(command, request.MessageNumber, payload);
    }

    private static Frame Ack(Frame request)
    {
        return Reply(request, Command.Ack, []);
    }

    private static Frame Nack(Frame request, NackCode code)
    {
        return Reply(request, Command.Nack, [(byte)code]);
    }
}

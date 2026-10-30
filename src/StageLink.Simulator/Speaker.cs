using StageLink.Protocol;

namespace StageLink.Simulator;

public class Speaker
{
    private const int FixedPointSize = 2;

    public const int MinTelemetryIntervalMs = 100;
    public const int MaxTelemetryIntervalMs = 10000;

    private readonly SpeakerState _state;

    public Speaker(SpeakerState state)
    {
        _state = state;
    }

    public Frame Handle(Frame request, Session session)
    {
        return request.Command switch
        {
            Command.Ping => Reply(request, Command.Pong, []),
            Command.GetState => HandleGetState(request),
            Command.SetGain => HandleFixedPointSetting(request, _state.SetGain),
            Command.SetMute => HandleSetMute(request),
            Command.SetTilt => HandleFixedPointSetting(request, _state.SetTilt),
            Command.Subscribe => HandleSubscribe(request, session),
            Command.Unsubscribe => HandleUnsubscribe(request, session),
            _ => Nack(request, NackCode.UnsupportedCommand),
        };
    }

    // Telemetry data: temperature (2), level (2), limiter (1), gain (2), mute (1), tilt (2), faults (1)
    public Frame BuildTelemetry(ushort messageNumber)
    {
        var writer = new ByteWriter();
        writer.WriteFixedPoint(_state.Temperature);
        writer.WriteFixedPoint(_state.Level);
        writer.WriteByte((byte)(_state.LimiterActive ? 1 : 0));
        writer.WriteFixedPoint(_state.Gain);
        writer.WriteByte((byte)(_state.Muted ? 1 : 0));
        writer.WriteFixedPoint(_state.Tilt);
        writer.WriteByte((byte)_state.Faults);

        return new Frame(Command.Telemetry, messageNumber, writer.ToArray());
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

    private static Frame HandleSubscribe(Frame request, Session session)
    {
        byte[] payload = request.Payload;
        if (payload.Length != 2)
        {
            return Nack(request, NackCode.InvalidPayload);
        }

        ushort milliseconds = new ByteReader(payload).ReadUInt16();
        if (milliseconds < MinTelemetryIntervalMs || milliseconds > MaxTelemetryIntervalMs)
        {
            return Nack(request, NackCode.ValueOutOfRange);
        }

        session.Subscribe(TimeSpan.FromMilliseconds(milliseconds), request.MessageNumber);
        return Ack(request);
    }

    private static Frame HandleUnsubscribe(Frame request, Session session)
    {
        if (request.Payload.Length != 0)
        {
            return Nack(request, NackCode.InvalidPayload);
        }

        session.Unsubscribe();
        return Ack(request);
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

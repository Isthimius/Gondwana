using System.Numerics;

namespace Gondwana.Demos.Breakout;

/// <summary>Platform-independent Breakout rules in logical 1920 x 1080 pixels.</summary>
internal sealed class BreakoutGame
{
    internal const int Width = 1920;
    internal const int Height = 1080;
    internal const int TileSize = 60;
    internal const float BallRadius = 13f;
    internal const float PaddleWidth = 210f;
    internal const float PaddleHeight = 30f;
    internal const float PaddleY = 946f;
    private const float PaddleSpeed = 1050f;
    private const float LeftWall = 40f;
    private const float RightWall = Width - LeftWall;
    private const float Ceiling = 120f;
    private const float MaximumBallSpeed = 1120f;

    private readonly List<Brick> _bricks = [];
    private Vector2 _velocity;

    internal IReadOnlyList<Brick> Bricks => _bricks;
    internal Vector2 Ball { get; private set; }
    internal float PaddleX { get; private set; }
    internal int Score { get; private set; }
    internal int Lives { get; private set; }
    internal GamePhase Phase { get; private set; } = GamePhase.Ready;

    internal BreakoutGame() => Reset();

    internal void Reset()
    {
        _bricks.Clear();
        const int columns = 12;
        const int rows = 6;
        const float width = 134f;
        const float height = 40f;
        const float gapX = 12f;
        const float gapY = 14f;
        float left = (Width - (columns * width + (columns - 1) * gapX)) / 2f;
        for (int row = 0; row < rows; row++)
            for (int col = 0; col < columns; col++)
                _bricks.Add(new Brick(left + col * (width + gapX),
                    166f + row * (height + gapY), width, height, row));

        Score = 0;
        Lives = 3;
        PaddleX = Width / 2f;
        ResetBall();
    }

    internal void Launch()
    {
        if (Phase != GamePhase.Ready) return;
        _velocity = new Vector2(350f, -570f);
        Phase = GamePhase.Playing;
    }

    internal void TogglePause()
    {
        Phase = Phase switch
        {
            GamePhase.Playing => GamePhase.Paused,
            GamePhase.Paused => GamePhase.Playing,
            _ => Phase
        };
    }

    internal void Update(float dt, float moveAxis)
    {
        dt = Math.Clamp(dt, 0f, 0.05f);
        PaddleX = Math.Clamp(PaddleX + Math.Clamp(moveAxis, -1f, 1f) * PaddleSpeed * dt,
            LeftWall + PaddleWidth / 2f, RightWall - PaddleWidth / 2f);

        if (Phase == GamePhase.Ready)
        {
            Ball = new Vector2(PaddleX, PaddleY - PaddleHeight / 2f - BallRadius - 5f);
            return;
        }
        if (Phase != GamePhase.Playing) return;

        // Short fixed substeps prevent tunneling through narrow bricks on slower machines.
        int steps = Math.Max(1, (int)MathF.Ceiling(dt / 0.008f));
        float step = dt / steps;
        for (int i = 0; i < steps && Phase == GamePhase.Playing; i++)
            AdvanceBall(step);
    }

    private void AdvanceBall(float dt)
    {
        Vector2 previous = Ball;
        Vector2 next = previous + _velocity * dt;

        if (next.X - BallRadius < LeftWall && _velocity.X < 0f)
        {
            next.X = LeftWall + BallRadius;
            _velocity.X = -_velocity.X;
        }
        else if (next.X + BallRadius > RightWall && _velocity.X > 0f)
        {
            next.X = RightWall - BallRadius;
            _velocity.X = -_velocity.X;
        }

        if (next.Y - BallRadius < Ceiling && _velocity.Y < 0f)
        {
            next.Y = Ceiling + BallRadius;
            _velocity.Y = -_velocity.Y;
        }

        float paddleTop = PaddleY - PaddleHeight / 2f;
        if (_velocity.Y > 0f &&
            previous.Y + BallRadius <= paddleTop &&
            next.Y + BallRadius >= paddleTop &&
            MathF.Abs(next.X - PaddleX) <= PaddleWidth / 2f + BallRadius)
        {
            next.Y = paddleTop - BallRadius;
            float offset = Math.Clamp((next.X - PaddleX) / (PaddleWidth / 2f), -1f, 1f);
            float angle = offset * 1.06f;
            float speed = Math.Min(_velocity.Length() * 1.025f, MaximumBallSpeed);
            _velocity = new Vector2(MathF.Sin(angle) * speed, -MathF.Cos(angle) * speed);
            if (MathF.Abs(_velocity.X) < 75f)
                _velocity.X = previous.X <= PaddleX ? -75f : 75f;
        }

        foreach (Brick brick in _bricks)
        {
            if (!brick.Alive ||
                next.X + BallRadius < brick.X || next.X - BallRadius > brick.X + brick.Width ||
                next.Y + BallRadius < brick.Y || next.Y - BallRadius > brick.Y + brick.Height)
                continue;

            brick.Alive = false;
            Score += (6 - brick.Row) * 25;
            if (previous.Y + BallRadius <= brick.Y && _velocity.Y > 0f)
            {
                next.Y = brick.Y - BallRadius;
                _velocity.Y = -MathF.Abs(_velocity.Y);
            }
            else if (previous.Y - BallRadius >= brick.Y + brick.Height && _velocity.Y < 0f)
            {
                next.Y = brick.Y + brick.Height + BallRadius;
                _velocity.Y = MathF.Abs(_velocity.Y);
            }
            else
            {
                _velocity.X = previous.X <= brick.X + brick.Width / 2f
                    ? -MathF.Abs(_velocity.X) : MathF.Abs(_velocity.X);
            }
            _velocity *= Math.Min(1.012f, MaximumBallSpeed / _velocity.Length());
            if (_bricks.TrueForAll(b => !b.Alive))
            {
                Phase = GamePhase.Victory;
                _velocity = Vector2.Zero;
            }
            break; // One brick per simulation substep.
        }

        Ball = next;
        if (Ball.Y - BallRadius > Height)
        {
            Lives--;
            if (Lives == 0)
            {
                Phase = GamePhase.GameOver;
                _velocity = Vector2.Zero;
            }
            else ResetBall();
        }
    }

    private void ResetBall()
    {
        _velocity = Vector2.Zero;
        Ball = new Vector2(PaddleX, PaddleY - PaddleHeight / 2f - BallRadius - 5f);
        Phase = GamePhase.Ready;
    }

    internal sealed class Brick(float x, float y, float width, float height, int row)
    {
        internal float X { get; } = x;
        internal float Y { get; } = y;
        internal float Width { get; } = width;
        internal float Height { get; } = height;
        internal int Row { get; } = row;
        internal bool Alive { get; set; } = true;
        internal Vector2 Center => new(X + Width / 2f, Y + Height / 2f);
    }

    internal enum GamePhase { Ready, Playing, Paused, Victory, GameOver }
}

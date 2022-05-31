using UnityEngine;
using System;
using System.Collections;

public class PlayerInput : SingletonMono<PlayerInput>
{
    public Joystick testJoystick;
    public Joystick Joytick
    {
        get => _joystick;
        set => _joystick = value;
    }
    private Joystick _joystick;
    protected Vector2 _movement;

    protected bool _jump;
    protected bool _attack;
    protected bool _pause;

    public Vector2 MoveInput => _movement;

    public bool Pause
    {
        get { return _pause; }
    }

    public override void Awake()
    {
        base.Awake();
    }

    void Update()
    {
        if(_joystick == null)
            return;

        _movement.Set(_joystick.Horizontal, _joystick.Vertical);
    }
}

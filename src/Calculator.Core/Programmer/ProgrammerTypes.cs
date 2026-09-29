namespace Calculator.Core.Programmer;

public enum Radix
{
    Hexadecimal = 16,
    Decimal = 10,
    Octal = 8,
    Binary = 2,
}

/// <summary>Number of bits of the value; values are stored in two's complement.</summary>
public enum WordSize
{
    QWord = 64,
    DWord = 32,
    Word = 16,
    Byte = 8,
}

/// <summary>How the shift keys work (legacy "Bit shift" flyout).</summary>
public enum ShiftMode
{
    /// <summary>&lt;&lt; and &gt;&gt; keep the sign.</summary>
    Arithmetic,

    /// <summary>&gt;&gt; fills with zeros.</summary>
    Logical,

    /// <summary>RoL / RoR rotate by one bit.</summary>
    Rotate,

    /// <summary>RcL / RcR rotate by one bit through the carry bit.</summary>
    RotateThroughCarry,
}

public enum ProgrammerOperator
{
    Add,
    Subtract,
    Multiply,
    Divide,
    Modulo,
    And,
    Or,
    Xor,
    Nand,
    Nor,

    /// <summary>x &lt;&lt; y.</summary>
    LeftShift,

    /// <summary>x &gt;&gt; y, arithmetic or logical depending on <see cref="ShiftMode"/>.</summary>
    RightShift,
}

public enum ProgrammerFunction
{
    Negate,
    Not,
    RotateLeft,
    RotateRight,
    RotateLeftThroughCarry,
    RotateRightThroughCarry,
}

public static class ProgrammerOperatorExtensions
{
    /// <summary>Precedence of the legacy engine (scicomm.cpp): OR/XOR &lt; AND/NAND/NOR &lt; +/− &lt; ×/÷/%/shifts.</summary>
    public static int Precedence(this ProgrammerOperator op) => op switch
    {
        ProgrammerOperator.Or or ProgrammerOperator.Xor => 0,
        ProgrammerOperator.And or ProgrammerOperator.Nand or ProgrammerOperator.Nor => 1,
        ProgrammerOperator.Add or ProgrammerOperator.Subtract => 2,
        _ => 3,
    };
}

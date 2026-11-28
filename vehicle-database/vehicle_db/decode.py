"""Safe PID formula decoding. Formulas may use only A..D, numbers, + - * / and parentheses."""
import ast, operator as op

_OPS = {ast.Add: op.add, ast.Sub: op.sub, ast.Mult: op.mul, ast.Div: op.truediv}

def safe_eval(formula, values):
    def ev(n):
        if isinstance(n, ast.Expression): return ev(n.body)
        if isinstance(n, ast.Constant) and isinstance(n.value, (int, float)) and not isinstance(n.value, bool): return n.value
        if isinstance(n, ast.Name) and n.id in values: return values[n.id]
        if isinstance(n, ast.BinOp) and type(n.op) in _OPS: return _OPS[type(n.op)](ev(n.left), ev(n.right))
        if isinstance(n, ast.UnaryOp) and isinstance(n.op, ast.USub): return -ev(n.operand)
        raise ValueError("unsupported expression element")
    return ev(ast.parse(formula, mode="eval"))

def decode_pid(row, data):
    """row: pid_definitions row/dict; data: response payload bytes (after service+pid)."""
    if len(data) < row["num_bytes"]:
        raise ValueError("response shorter than PID definition")
    return safe_eval(row["formula"], {n: data[i] for i, n in enumerate("ABCD"[:row["num_bytes"]])})

def pid_support_location(pid):
    """Where a Mode 01 PID's support flag lives: (bitmap_pid, bit). bit 31 = first PID after the bitmap PID, 0 = base+32.
    PID 0x00 is the root of the chain and has no location (ValueError)."""
    if not isinstance(pid, int) or not 1 <= pid <= 255:
        raise ValueError("pid must be 1..255")
    base = ((pid - 1) // 32) * 32
    return base, 32 - (pid - base)

def decode_supported_pids(bitmap_pid, data):
    """data = 4 response bytes of PID 0x00/0x20/0x40/...; returns the sorted list of PIDs flagged as supported."""
    if len(data) < 4: raise ValueError("support bitmap needs 4 bytes")
    if bitmap_pid % 32 != 0: raise ValueError("bitmap_pid must be a multiple of 32")
    value = int.from_bytes(bytes(data[:4]), "big")
    return [bitmap_pid + k for k in range(1, 33) if value & (1 << (32 - k))]

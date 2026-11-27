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

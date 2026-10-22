using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace HongBallFight
{
    public sealed class ScriptError : Exception
    {
        public ScriptError(int line, int column, string message) : base($"{line}행 {column}열: {message}") { }
    }

    public interface ISkillHost
    {
        object Read(string name);
        object Call(string name, object[] arguments);
    }

    public sealed class SkillScript
    {
        public const int MaxSourceLength = 24000;
        public const int OperationLimit = 5000;
        public static readonly string[] Events = { "Start", "Update", "FixedUpdate", "OnSkill", "OnCollision", "OnWallHit", "OnDamage", "OnDeath", "OnProjectileHit" };
        public static readonly string[] Properties = { "Time.deltaTime", "Time.time", "Self.HP", "Self.MaxHP", "Self.X", "Self.Y", "Self.Speed", "Self.Attack", "Self.SkillPower", "Self.Shield", "Self.Cooldown", "Enemy.HP", "Enemy.MaxHP", "Enemy.X", "Enemy.Y", "Arena.Width", "Arena.Height", "Mathf.PI", "Hit.Damage", "Hit.Projectile" };
        public sealed class Function
        {
            public string Name;
            public string[] Parameters;
            internal Node Body;
        }
        public readonly Dictionary<string, Function> Functions = new Dictionary<string, Function>();
        readonly List<Node> initializers = new List<Node>();
        readonly HashSet<string> globals = new HashSet<string>();

        public static SkillScript Compile(string source)
        {
            if ((source ?? "").Length > MaxSourceLength) throw new ScriptError(1, 1, "코드는 24000자 이하여야 합니다.");
            var parser = new Parser(source ?? "");
            return parser.Parse();
        }
        public static string Validate(string source)
        {
            try { Compile(source); return null; } catch (ScriptError error) { return error.Message; }
        }
        public Runtime CreateRuntime(ISkillHost host) => new Runtime(this, host);

        public sealed class Runtime
        {
            readonly SkillScript program;
            readonly ISkillHost host;
            readonly Dictionary<string, object> state = new Dictionary<string, object>();
            readonly Stack<Dictionary<string, object>> frames = new Stack<Dictionary<string, object>>();
            int operations, depth;
            internal Runtime(SkillScript program, ISkillHost host)
            {
                this.program = program; this.host = host;
                operations = OperationLimit;
                foreach (var name in program.globals) state[name] = 0d;
                foreach (var node in program.initializers) node.Run(this);
            }
            public bool Has(string name) => program.Functions.ContainsKey(name);
            public void Invoke(string name)
            {
                if (!program.Functions.ContainsKey(name)) return;
                operations = OperationLimit;
                Call(name, Array.Empty<object>(), new Token(name, 1, 1));
            }
            internal void Spend(Token token)
            {
                if (--operations < 0) throw new ScriptError(token.Line, token.Column, "실행 예산 초과. 무한 반복이나 과도한 연산을 줄이세요.");
            }
            internal object Read(Token token)
            {
                Spend(token);
                if (frames.Count > 0 && frames.Peek().TryGetValue(token.Text, out var local)) return local;
                if (state.TryGetValue(token.Text, out var global)) return global;
                try { return host.Read(token.Text); }
                catch (Exception error) { throw new ScriptError(token.Line, token.Column, error.Message); }
            }
            internal void Write(Token token, object value, bool declare)
            {
                Spend(token);
                if (token.Text.Contains(".")) throw new ScriptError(token.Line, token.Column, "속성은 읽기 전용입니다. 전용 함수를 사용하세요.");
                if (frames.Count == 0) { state[token.Text] = value; return; }
                if (declare || frames.Peek().ContainsKey(token.Text)) frames.Peek()[token.Text] = value;
                else if (state.ContainsKey(token.Text)) state[token.Text] = value;
                else throw new ScriptError(token.Line, token.Column, "선언되지 않은 변수: " + token.Text);
            }
            internal object Call(string name, object[] args, Token token)
            {
                Spend(token);
                if (!program.Functions.TryGetValue(name, out var function))
                {
                    try { return host.Call(name, args); }
                    catch (Exception error) { throw new ScriptError(token.Line, token.Column, error.Message); }
                }
                if (++depth > 16) { depth--; throw new ScriptError(token.Line, token.Column, "함수 호출 깊이 초과"); }
                var locals = new Dictionary<string, object>();
                for (int i = 0; i < function.Parameters.Length; i++) locals[function.Parameters[i]] = i < args.Length ? args[i] : 0d;
                frames.Push(locals);
                try { return function.Body.Run(this); }
                catch (ReturnValue result) { return result.Value; }
                finally { frames.Pop(); depth--; }
            }
        }

        internal sealed class Token
        {
            public string Text;
            public int Line, Column;
            public bool IsString;
            public Token(string text, int line, int column, bool isString = false) { Text = text; Line = line; Column = column; IsString = isString; }
        }
        internal sealed class ReturnValue : Exception { public object Value; public ReturnValue(object value) { Value = value; } }
        internal sealed class LoopBreak : Exception { }
        internal sealed class LoopContinue : Exception { }
        internal abstract class Node
        {
            public Token Token;
            protected Node(Token token) { Token = token; }
            public abstract object Run(Runtime runtime);
        }
        sealed class Literal : Node
        {
            readonly object value;
            public Literal(Token token, object value) : base(token) { this.value = value; }
            public override object Run(Runtime runtime) { runtime.Spend(Token); return value; }
        }
        sealed class Variable : Node
        {
            public Variable(Token token) : base(token) { }
            public override object Run(Runtime runtime) => runtime.Read(Token);
        }
        sealed class Assignment : Node
        {
            readonly Node value;
            readonly string operation;
            readonly bool declare;
            readonly string compound;
            public Assignment(Token token, Node value, string operation, bool declare) : base(token) { this.value = value; this.operation = operation; this.declare = declare; compound = operation == "=" ? null : operation.Substring(0, 1); }
            public override object Run(Runtime runtime)
            {
                object next = value.Run(runtime);
                if (compound != null) next = Binary.Calculate(compound, runtime.Read(Token), next);
                runtime.Write(Token, next, declare); return next;
            }
        }
        sealed class Binary : Node
        {
            readonly Node left, right;
            public Binary(Token token, Node left, Node right) : base(token) { this.left = left; this.right = right; }
            public static object Calculate(string op, object a, object b)
            {
                if (op == "+" && (a is string || b is string))
                {
                    string text = Convert.ToString(a, CultureInfo.InvariantCulture) + Convert.ToString(b, CultureInfo.InvariantCulture);
                    if (text.Length > 2048) throw new Exception("문자열은 2048자 이하여야 합니다.");
                    return text;
                }
                if (op == "==") return Equals(a, b);
                if (op == "!=") return !Equals(a, b);
                double x = Number(a), y = Number(b);
                switch (op)
                {
                    case "+": return x + y; case "-": return x - y; case "*": return x * y;
                    case "/": return y == 0 ? 0d : x / y;
                    case "%": return y == 0 ? 0d : x % y;
                    case "<": return x < y; case ">": return x > y; case "<=": return x <= y; case ">=": return x >= y;
                    default: throw new Exception("알 수 없는 연산자 " + op);
                }
            }
            public override object Run(Runtime runtime)
            {
                runtime.Spend(Token);
                var a = left.Run(runtime);
                if (Token.Text == "&&") return Truth(a) && Truth(right.Run(runtime));
                if (Token.Text == "||") return Truth(a) || Truth(right.Run(runtime));
                try { return Calculate(Token.Text, a, right.Run(runtime)); }
                catch (ScriptError) { throw; }
                catch (Exception error) { throw new ScriptError(Token.Line, Token.Column, error.Message); }
            }
        }
        sealed class Unary : Node
        {
            readonly Node value;
            public Unary(Token token, Node value) : base(token) { this.value = value; }
            public override object Run(Runtime runtime)
            {
                runtime.Spend(Token); var result = value.Run(runtime);
                return Token.Text == "!" ? (object)!Truth(result) : Token.Text == "-" ? -Number(result) : Number(result);
            }
        }
        sealed class CallNode : Node
        {
            public readonly List<Node> Args;
            public CallNode(Token token, List<Node> args) : base(token) { Args = args; }
            public override object Run(Runtime runtime)
            {
                var values = new object[Args.Count];
                for (int i = 0; i < values.Length; i++) values[i] = Args[i].Run(runtime);
                return runtime.Call(Token.Text, values, Token);
            }
        }
        sealed class Block : Node
        {
            readonly List<Node> nodes;
            public Block(Token token, List<Node> nodes) : base(token) { this.nodes = nodes; }
            public override object Run(Runtime runtime) { runtime.Spend(Token); foreach (var node in nodes) node.Run(runtime); return 0d; }
        }
        sealed class IfNode : Node
        {
            readonly Node condition, yes, no;
            public IfNode(Token token, Node condition, Node yes, Node no) : base(token) { this.condition = condition; this.yes = yes; this.no = no; }
            public override object Run(Runtime runtime) { runtime.Spend(Token); return Truth(condition.Run(runtime)) ? yes.Run(runtime) : no?.Run(runtime) ?? 0d; }
        }
        sealed class LoopNode : Node
        {
            readonly Node initialize, condition, advance, body;
            public LoopNode(Token token, Node initialize, Node condition, Node advance, Node body) : base(token)
            { this.initialize = initialize; this.condition = condition; this.advance = advance; this.body = body; }
            public override object Run(Runtime runtime)
            {
                initialize?.Run(runtime);
                while (condition == null || Truth(condition.Run(runtime)))
                {
                    runtime.Spend(Token);
                    try { body.Run(runtime); } catch (LoopBreak) { break; } catch (LoopContinue) { }
                    advance?.Run(runtime);
                }
                return 0d;
            }
        }
        sealed class Control : Node
        {
            readonly Node value;
            public Control(Token token, Node value) : base(token) { this.value = value; }
            public override object Run(Runtime runtime)
            {
                runtime.Spend(Token);
                if (Token.Text == "break") throw new LoopBreak();
                if (Token.Text == "continue") throw new LoopContinue();
                throw new ReturnValue(value?.Run(runtime) ?? 0d);
            }
        }
        public static double Number(object value)
        {
            double number = Convert.ToDouble(value, CultureInfo.InvariantCulture);
            if (double.IsNaN(number) || double.IsInfinity(number)) throw new Exception("유한한 숫자가 필요합니다.");
            return number;
        }
        public static bool Truth(object value) => value is bool boolean ? boolean : value is string text ? text.Length > 0 : Number(value) != 0;

        sealed class Parser
        {
            readonly List<Token> tokens = new List<Token>();
            readonly List<CallNode> calls = new List<CallNode>();
            readonly SkillScript program = new SkillScript();
            int position, nesting, loops;
            Token Current => tokens[position];
            static bool Type(string value) => value == "float" || value == "int" || value == "bool" || value == "string" || value == "double" || value == "var" || value == "void";
            public Parser(string source)
            {
                int line = 1, column = 1;
                for (int i = 0; i < source.Length;)
                {
                    char ch = source[i];
                    if (char.IsWhiteSpace(ch)) { i++; if (ch == '\n') { line++; column = 1; } else column++; continue; }
                    int startLine = line, startColumn = column, start = i;
                    if (ch == '/' && i + 1 < source.Length && source[i + 1] == '/')
                    { while (i < source.Length && source[i] != '\n') { i++; column++; } continue; }
                    if (ch == '"')
                    {
                        i++; column++; var text = new StringBuilder(); bool closed = false;
                        while (i < source.Length)
                        {
                            char c = source[i++]; column++;
                            if (c == '"') { closed = true; break; }
                            if (c == '\n') throw new ScriptError(line, column, "문자열 안에서는 \\n을 사용하세요.");
                            if (c == '\\' && i < source.Length)
                            {
                                c = source[i++]; column++;
                                if (c == 'u')
                                {
                                    if (i + 4 > source.Length || !ushort.TryParse(source.Substring(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var unicode)) throw new ScriptError(line, column, "유니코드 이스케이프 오류");
                                    c = (char)unicode; i += 4; column += 4;
                                }
                                else c = c == 'n' ? '\n' : c == 't' ? '\t' : c == 'r' ? '\r' : c == 'b' ? '\b' : c == 'f' ? '\f' : c;
                            }
                            text.Append(c);
                        }
                        if (!closed) throw new ScriptError(startLine, startColumn, "문자열의 닫는 따옴표가 없습니다.");
                        tokens.Add(new Token(text.ToString(), startLine, startColumn, true)); continue;
                    }
                    if (char.IsLetter(ch) || ch == '_')
                    {
                        while (i < source.Length && (char.IsLetterOrDigit(source[i]) || source[i] == '_' || source[i] == '.')) { i++; column++; }
                    }
                    else if (char.IsDigit(ch) || ch == '.' && i + 1 < source.Length && char.IsDigit(source[i + 1]))
                    {
                        while (i < source.Length && (char.IsDigit(source[i]) || source[i] == '.')) { i++; column++; }
                        if (i < source.Length && source[i] == 'f') { i++; column++; }
                    }
                    else
                    {
                        i++; column++;
                        if (i < source.Length && new[] { "==", "!=", ">=", "<=", "&&", "||", "+=", "-=", "*=", "/=", "++", "--" }.Contains(source.Substring(start, 2))) { i++; column++; }
                    }
                    tokens.Add(new Token(source.Substring(start, i - start), startLine, startColumn));
                    if (tokens.Count > 7000) throw new ScriptError(line, column, "토큰 수 초과");
                }
                tokens.Add(new Token("<end>", line, column));
            }
            Token Take() => tokens[position++];
            bool Match(string value) { if (!Current.IsString && Current.Text == value) { position++; return true; } return false; }
            Token Need(string value) { if (!Current.IsString && Current.Text == value) return Take(); throw Error("필요한 기호: " + value); }
            ScriptError Error(string message) => new ScriptError(Current.Line, Current.Column, message);
            Token Identifier()
            {
                if (Current.IsString || !(char.IsLetter(Current.Text[0]) || Current.Text[0] == '_') || Type(Current.Text)) throw Error("이름이 필요합니다.");
                return Take();
            }
            public SkillScript Parse()
            {
                while (Current.Text != "<end>")
                {
                    Match("public"); Match("private");
                    if (!Type(Current.Text)) throw Error("전역 변수 또는 함수 선언이 필요합니다. 예: void Update() { }");
                    string type = Take().Text; var name = Identifier();
                    if (Match("("))
                    {
                        var parameters = new List<string>();
                        if (!Match(")"))
                        {
                            do { if (!Type(Current.Text) || Current.Text == "void") throw Error("인자 타입이 필요합니다."); Take(); parameters.Add(Identifier().Text); } while (Match(","));
                            Need(")");
                        }
                        if (parameters.Distinct().Count() != parameters.Count || program.Functions.ContainsKey(name.Text) || SkillApi.Find(name.Text) != null) throw Error("중복 함수 또는 인자 이름");
                        if (Events.Contains(name.Text) && parameters.Count != 0) throw Error("이벤트 함수는 인자 없이 선언하세요. Hit 속성으로 충돌 정보를 읽습니다.");
                        program.Functions[name.Text] = new Function { Name = name.Text, Parameters = parameters.ToArray(), Body = Statement() };
                    }
                    else
                    {
                        if (type == "void" || !program.globals.Add(name.Text)) throw Error("잘못된 전역 변수 선언");
                        program.initializers.Add(new Assignment(name, Match("=") ? Expression() : new Literal(name, type == "string" ? (object)"" : type == "bool" ? false : 0d), "=", true)); Need(";");
                    }
                }
                foreach (var call in calls)
                {
                    int count = program.Functions.TryGetValue(call.Token.Text, out var function) ? function.Parameters.Length : SkillApi.Find(call.Token.Text)?.Arguments ?? -1;
                    if (count < 0) throw new ScriptError(call.Token.Line, call.Token.Column, "알 수 없는 함수: " + call.Token.Text);
                    if (call.Args.Count != count) throw new ScriptError(call.Token.Line, call.Token.Column, $"{call.Token.Text}: 인자 {count}개 필요");
                }
                return program;
            }
            Node Statement()
            {
                if (++nesting > 48) throw Error("블록 중첩 한도 초과");
                try
                {
                    var token = Current;
                    if (Match("{"))
                    {
                        var nodes = new List<Node>();
                        while (!Match("}")) { if (Current.Text == "<end>") throw Error("닫는 중괄호가 없습니다."); nodes.Add(Statement()); }
                        return new Block(token, nodes);
                    }
                    if (Match("if")) { Need("("); var condition = Expression(); Need(")"); var yes = Statement(); return new IfNode(token, condition, yes, Match("else") ? Statement() : null); }
                    if (Match("for"))
                    {
                        Need("("); var init = Current.Text == ";" ? null : Simple(); Need(";");
                        var condition = Current.Text == ";" ? null : Expression(); Need(";");
                        var advance = Current.Text == ")" ? null : Simple(); Need(")");
                        loops++; var body = Statement(); loops--; return new LoopNode(token, init, condition, advance, body);
                    }
                    if (Match("while")) { Need("("); var condition = Expression(); Need(")"); loops++; var body = Statement(); loops--; return new LoopNode(token, null, condition, null, body); }
                    if (Match("return")) { var value = Current.Text == ";" ? null : Expression(); Need(";"); return new Control(token, value); }
                    if (Match("break") || Match("continue")) { if (loops == 0) throw Error("반복문 안에서 사용하세요."); Need(";"); return new Control(token, null); }
                    if (Match(";")) return new Literal(token, 0d);
                    var node = Simple(); Need(";"); return node;
                }
                finally { nesting--; }
            }
            Node Simple()
            {
                bool declare = Type(Current.Text);
                if (declare) Take();
                if (declare)
                {
                    var name = Identifier(); return new Assignment(name, Match("=") ? Expression() : new Literal(name, 0d), "=", true);
                }
                var expression = Expression();
                if (Current.Text == "=" || Current.Text == "+=" || Current.Text == "-=" || Current.Text == "*=" || Current.Text == "/=" || Current.Text == "++" || Current.Text == "--")
                {
                    if (!(expression is Variable)) throw Error("변수에만 대입할 수 있습니다.");
                    var op = Take(); return new Assignment(expression.Token, op.Text == "++" || op.Text == "--" ? new Literal(op, 1d) : Expression(), op.Text == "++" ? "+=" : op.Text == "--" ? "-=" : op.Text, false);
                }
                return expression;
            }
            static int Precedence(string op)
            {
                switch (op) { case "||": return 1; case "&&": return 2; case "==": case "!=": return 3; case "<": case ">": case "<=": case ">=": return 4; case "+": case "-": return 5; case "*": case "/": case "%": return 6; default: return 0; }
            }
            Node Expression(int minimum = 1)
            {
                if (++nesting > 48) throw Error("수식 중첩 한도 초과");
                try
                {
                    Node left;
                    var token = Take();
                    if (token.IsString) left = new Literal(token, token.Text);
                    else if (token.Text == "!" || token.Text == "-" || token.Text == "+") left = new Unary(token, Expression(7));
                    else if (token.Text == "(") { left = Expression(); Need(")"); }
                    else if (token.Text == "true" || token.Text == "false") left = new Literal(token, token.Text == "true");
                    else if (double.TryParse(token.Text.TrimEnd('f'), NumberStyles.Float, CultureInfo.InvariantCulture, out var number) && double.IsFinite(number)) left = new Literal(token, number);
                    else if (char.IsLetter(token.Text[0]) || token.Text[0] == '_')
                    {
                        if (Match("("))
                        {
                            var args = new List<Node>();
                            if (!Match(")")) { do { args.Add(Expression()); } while (Match(",")); Need(")"); }
                            var call = new CallNode(token, args); calls.Add(call); left = call;
                        }
                        else left = new Variable(token);
                    }
                    else throw new ScriptError(token.Line, token.Column, "올바른 수식이 필요합니다.");
                    while (!Current.IsString && Precedence(Current.Text) >= minimum)
                    {
                        var op = Take(); left = new Binary(op, left, Expression(Precedence(op.Text) + 1));
                    }
                    return left;
                }
                finally { nesting--; }
            }
        }
    }
}

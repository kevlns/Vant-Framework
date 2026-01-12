using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Vant.Utils
{
    /// <summary>
    /// 表达式计算辅助类
    /// 支持逻辑运算符: && (AND), || (OR), ( )
    /// 支持比较运算符: =, !=, >, <, >=, <=
    /// </summary>
    public static class ExpressionHelper
    {
        public delegate object ContextProvider(string variableName);

        // 用于匹配原子表达式: Key Operator Value (例如: Level >= 10)
        // Group 1: Key, Group 2: Operator, Group 3: Value
        private static readonly Regex AtomRegex = new Regex(@"^(.+?)(>=|<=|==|!=|>|<|=)(.+)$", RegexOptions.Compiled | RegexOptions.Singleline);

        /// <summary>
        /// 计算布尔表达式的值
        /// </summary>
        /// <param name="expression">表达式字符串, 例如: "Level >= 10 && (Type = 1 || Type = 2)"</param>
        /// <param name="provider">变量值获取器，默认使用 ConditionContext.GetData</param>
        /// <returns>表达式结果</returns>
        public static bool Evaluate(string expression, ContextProvider provider = null)
        {
            if (string.IsNullOrWhiteSpace(expression)) return true; // 空表达式默认通过

            try
            {
                var tokens = Tokenize(expression);
                return EvaluateReversePolish(ShuntingYard(tokens), provider ?? ConditionContext.GetData);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ExpressionHelper] Evaluate Error: {expression} . Exception: {ex}");
                return false;
            }
        }

        #region Private Implementation

        private const string OP_AND = "&&";
        private const string OP_OR = "||";
        private const string OP_LEFT = "(";
        private const string OP_RIGHT = ")";

        // 1. 分词：将表达式拆解为 Token
        private static List<string> Tokenize(string expression)
        {
            // 在操作符周围添加分隔符，方便Split
            // 使用特殊字符作为一个临时Token分隔符
            string s = expression.Replace("&&", " \u0001&&\u0001 ")
                                 .Replace("||", " \u0001||\u0001 ")
                                 .Replace("(", " \u0001(\u0001 ")
                                 .Replace(")", " \u0001)\u0001 ");

            var rawTokens = s.Split(new[] { '\u0001' }, StringSplitOptions.RemoveEmptyEntries);
            var tokens = new List<string>();

            foreach (var t in rawTokens)
            {
                var trimmed = t.Trim();
                if (!string.IsNullOrEmpty(trimmed))
                {
                    tokens.Add(trimmed);
                }
            }
            return tokens;
        }

        // 2. 调度场算法 (Shunting-yard algorithm)：中缀表达式转后缀表达式 (RPN)
        private static Queue<string> ShuntingYard(List<string> tokens)
        {
            var outputQueue = new Queue<string>();
            var operatorStack = new Stack<string>();

            var precedence = new Dictionary<string, int>
            {
                { OP_OR, 1 },
                { OP_AND, 2 },
                { OP_LEFT, 0 }
            };

            foreach (var token in tokens)
            {
                if (token == OP_AND || token == OP_OR)
                {
                    while (operatorStack.Count > 0 && precedence[operatorStack.Peek()] >= precedence[token])
                    {
                        outputQueue.Enqueue(operatorStack.Pop());
                    }
                    operatorStack.Push(token);
                }
                else if (token == OP_LEFT)
                {
                    operatorStack.Push(token);
                }
                else if (token == OP_RIGHT)
                {
                    while (operatorStack.Count > 0 && operatorStack.Peek() != OP_LEFT)
                    {
                        outputQueue.Enqueue(operatorStack.Pop());
                    }
                    
                    if (operatorStack.Count > 0) operatorStack.Pop(); // Pop "("
                }
                else
                {
                    // 操作数 (原子表达式)
                    outputQueue.Enqueue(token);
                }
            }

            while (operatorStack.Count > 0)
            {
                outputQueue.Enqueue(operatorStack.Pop());
            }

            return outputQueue;
        }

        // 3. 计算后缀表达式
        private static bool EvaluateReversePolish(Queue<string> rpnQueue, ContextProvider provider)
        {
            var stack = new Stack<bool>();

            foreach (var token in rpnQueue)
            {
                if (token == OP_AND)
                {
                    var b = stack.Pop();
                    var a = stack.Pop();
                    stack.Push(a && b);
                }
                else if (token == OP_OR)
                {
                    var b = stack.Pop();
                    var a = stack.Pop();
                    stack.Push(a || b);
                }
                else
                {
                    // 原子表达式求值
                    stack.Push(EvaluateAtom(token, provider));
                }
            }

            return stack.Count > 0 && stack.Pop();
        }

        // 4. 原子表达式计算 (A=B)
        private static bool EvaluateAtom(string atom, ContextProvider provider)
        {
            var match = AtomRegex.Match(atom);
            if (!match.Success)
            {
                // 如果不是 A=B 格式，可能是单个布尔变量
                var val = provider(atom);
                if (val is bool bVal) return bVal;
                // 尝试解析字符串 True/False
                if (bool.TryParse(atom, out var parsedBool)) return parsedBool;
                return false;
            }

            string key = match.Groups[1].Value.Trim();
            string op = match.Groups[2].Value.Trim();
            string targetValueStr = match.Groups[3].Value.Trim();

            // 获取左值 (从 Provider)
            object leftValue = provider(key);
            if (leftValue == null) return false;

            // 比较
            return Compare(leftValue, op, targetValueStr);
        }

        private static bool Compare(object left, string op, string rightStr)
        {
            // 尝试统一类型为 left 的类型进行比较
            try
            {
                IComparable cLeft = left as IComparable;
                IComparable cRight = null;

                // 类型转换
                if (left is int || left is long || left is short || left is byte)
                {
                    cLeft = Convert.ToDouble(left); // 转为 Double 比较以兼容不同整型
                    if (double.TryParse(rightStr, out var dVal)) cRight = dVal;
                }
                else if (left is float || left is double)
                {
                    cLeft = Convert.ToDouble(left);
                    if (double.TryParse(rightStr, out var dVal)) cRight = dVal;
                }
                else if (left is string)
                {
                    // 字符串去除可能的引号
                    cRight = rightStr.Trim('\'', '\"');
                }
                else if (left is bool)
                {
                    if (bool.TryParse(rightStr, out var bVal)) cRight = bVal;
                }
                else if (left is Enum)
                {
                    // 枚举处理
                    cLeft = (int)left;
                    if (int.TryParse(rightStr, out var iEnumVal)) cRight = iEnumVal;
                    else
                    {
                        // 尝试字符串枚举解析
                        try { cRight = (int)Enum.Parse(left.GetType(), rightStr); } catch { }
                    }
                }

                if (cLeft == null || cRight == null) return false;

                int result = cLeft.CompareTo(cRight);

                switch (op)
                {
                    case "=":
                    case "==": return result == 0;
                    case "!=": return result != 0;
                    case ">": return result > 0;
                    case ">=": return result >= 0;
                    case "<": return result < 0;
                    case "<=": return result <= 0;
                    default: return false;
                }
            }
            catch
            {
                return false;
            }
        }

        #endregion
    }
}
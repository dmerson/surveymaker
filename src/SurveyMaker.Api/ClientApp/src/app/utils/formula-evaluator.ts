import { FormulaToken } from '../models/survey.model';

export function evaluateFormula(
  tokens: FormulaToken[],
  getQuestionValue: (id: number) => number
): number {
  let pos = 0;

  const peek = (): FormulaToken | undefined => tokens[pos];
  const consume = (): FormulaToken => tokens[pos++];

  function parseExpression(): number {
    let left = parseTerm();
    while (pos < tokens.length) {
      const t = peek();
      if (t?.type === 'op' && (t.value === '+' || t.value === '-')) {
        consume();
        const right = parseTerm();
        left = t.value === '+' ? left + right : left - right;
      } else break;
    }
    return left;
  }

  function parseTerm(): number {
    let left = parseUnary();
    while (pos < tokens.length) {
      const t = peek();
      if (t?.type === 'op' && (t.value === '*' || t.value === '/' || t.value === 'mod')) {
        consume();
        const right = parseUnary();
        if (t.value === '*') left *= right;
        else if (t.value === '/') left = right !== 0 ? left / right : NaN;
        else left = left % right;
      } else break;
    }
    return left;
  }

  function parseUnary(): number {
    const t = peek();
    if (t?.type === 'op' && t.value === '-') {
      consume();
      return -parseUnary();
    }
    return parsePrimary();
  }

  function parsePrimary(): number {
    const t = peek();
    if (!t) return NaN;

    if (t.type === 'number') { consume(); return t.value; }

    if (t.type === 'question') { consume(); return getQuestionValue(t.id); }

    if (t.type === 'fn') {
      consume();
      const lp = peek();
      if (lp?.type !== 'paren' || lp.value !== '(') return NaN;
      consume();
      const args = parseArgList();
      const rp = peek();
      if (rp?.type === 'paren' && rp.value === ')') consume();

      switch (t.value) {
        case 'sqrt': return Math.sqrt(args[0] ?? 0);
        case 'abs':  return Math.abs(args[0] ?? 0);
        case 'mean': return args.length > 0 ? args.reduce((a, b) => a + b, 0) / args.length : NaN;
        case 'max':  return args.length > 0 ? Math.max(...args) : NaN;
        case 'min':  return args.length > 0 ? Math.min(...args) : NaN;
        default:     return NaN;
      }
    }

    if (t.type === 'paren' && t.value === '(') {
      consume();
      const val = parseExpression();
      const rp = peek();
      if (rp?.type === 'paren' && rp.value === ')') consume();
      return val;
    }

    return NaN;
  }

  function parseArgList(): number[] {
    const args: number[] = [];
    const first = peek();
    if (first?.type === 'paren' && first.value === ')') return args;
    args.push(parseExpression());
    while (pos < tokens.length) {
      const sep = peek();
      if (sep?.type === 'op' && sep.value === ',') {
        consume();
        args.push(parseExpression());
      } else break;
    }
    return args;
  }

  try {
    const result = parseExpression();
    return isFinite(result) ? result : NaN;
  } catch {
    return NaN;
  }
}

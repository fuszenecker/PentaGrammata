#!/usr/bin/env node

'use strict';

const readline = require('node:readline');

function usage() {
  console.error('Usage: node superencode.js <enc|dec> <KEY>');
  console.error('       node superencode.js repl [KEY]   (interactive session)');
  console.error('Reads input from stdin and writes result to stdout.');
}

function isAlphabetic(str) {
  return /^[A-Za-z]+$/.test(str);
}

function keyOrder(key) {
  // Stable alphabetical ordering: if letters are equal, lower index comes first.
  return key
    .split('')
    .map((ch, index) => ({ ch: ch.toLowerCase(), index }))
    .sort((a, b) => {
      if (a.ch < b.ch) return -1;
      if (a.ch > b.ch) return 1;
      return a.index - b.index;
    })
    .map((item) => item.index);
}

// Word gaps ride along as "+" so they survive as a sendable character. These two
// tables must stay exact inverses of each other or enc | dec stops round-tripping.
// "." is passed through unchanged (send it with the "Full punctuation" set).
function normalizePlainText(input) {
  // Any run of whitespace, including line breaks, is one word separator. Trimmed
  // first so a trailing newline from a shell pipe does not become a word gap.
  return input.trim().replace(/\s+/g, ' ').replace(/ /g, '+');
}

function denormalizePlainText(input) {
  return input.replace(/\+/g, ' ');
}

function splitIntoGroups(text, groupSize = 5) {
  if (text.length === 0) {
    return '';
  }

  const groups = [];
  for (let i = 0; i < text.length; i += groupSize) {
    groups.push(text.slice(i, i + groupSize));
  }

  return groups.join(' ');
}

function encrypt(plainText, key) {
  const cols = key.length;
  const normalized = normalizePlainText(plainText);

  if (normalized.length === 0) {
    return '';
  }

  const rows = Math.ceil(normalized.length / cols);

  const table = [];
  for (let r = 0; r < rows; r += 1) {
    table.push(normalized.slice(r * cols, (r + 1) * cols).split(''));
  }

  const order = keyOrder(key);
  let out = '';
  for (const colIndex of order) {
    for (let r = 0; r < rows; r += 1) {
      if (table[r][colIndex] !== undefined) {
        out += table[r][colIndex];
      }
    }
  }

  return splitIntoGroups(out, 5);
}

function decrypt(cipherText, key) {
  const cols = key.length;
  const input = cipherText.replace(/\s+/g, '');

  if (input.length === 0) {
    return '';
  }

  const rows = Math.ceil(input.length / cols);
  const remainder = input.length % cols;
  const order = keyOrder(key);

  const colData = new Array(cols);
  let pos = 0;
  for (const colIndex of order) {
    const colLength =
      remainder === 0 || colIndex < remainder ? rows : rows - 1;
    colData[colIndex] = input.slice(pos, pos + colLength).split('');
    pos += colLength;
  }

  let out = '';
  for (let r = 0; r < rows; r += 1) {
    for (let c = 0; c < cols; c += 1) {
      if (colData[c][r] !== undefined) {
        out += colData[c][r];
      }
    }
  }

  return denormalizePlainText(out);
}

const REPL_HELP = `REPL commands:
  key <KEY>        set the transposition key (letters only)
  key              show the current key
  enc <text>       encrypt text with the current key
  dec <text>       decrypt text with the current key
  help             show this help
  exit | quit      leave (Ctrl-C / Ctrl-D also work)

The text after enc/dec is the message; spaces are allowed and become word gaps.
Encrypted output is 5-char groups separated by spaces — paste those groups
straight back into dec.`;

function startRepl(initialKey) {
  let key = null;
  if (initialKey) {
    if (!isAlphabetic(initialKey)) {
      console.error('Error: key must contain alphabetic characters only.');
      process.exit(1);
    }
    key = initialKey;
  }

  const rl = readline.createInterface({
    input: process.stdin,
    output: process.stdout,
    prompt: 'super> ',
  });

  console.log('Super-encode REPL — "help" for commands, "exit" or Ctrl-D to quit.');
  if (key) console.log(`key set: ${key} (${key.length} cols)`);
  else console.log('No key set. Start with: key <KEY>');
  rl.prompt();

  rl.on('line', (line) => {
    const trimmed = line.trim();
    if (!trimmed) { rl.prompt(); return; }
    const sp = trimmed.indexOf(' ');
    const cmd = sp === -1 ? trimmed : trimmed.slice(0, sp);
    const arg = sp === -1 ? '' : trimmed.slice(sp + 1);

    switch (cmd) {
      case 'key':
        if (!arg) { console.log(key ? `key: ${key} (${key.length} cols)` : '(no key set)'); break; }
        if (!isAlphabetic(arg)) { console.log('Error: key must contain alphabetic characters only.'); break; }
        key = arg;
        console.log(`key set: ${key} (${key.length} cols)`);
        break;
      case 'enc':
      case 'encrypt':
        if (!key) { console.log('No key set. Use: key <KEY>'); break; }
        try { console.log(encrypt(arg, key)); } catch (err) { console.log(`Error: ${err.message}`); }
        break;
      case 'dec':
      case 'decrypt':
        if (!key) { console.log('No key set. Use: key <KEY>'); break; }
        try { console.log(decrypt(arg, key)); } catch (err) { console.log(`Error: ${err.message}`); }
        break;
      case 'help':
        console.log(REPL_HELP);
        break;
      case 'exit':
      case 'quit':
        rl.close();
        return;
      default:
        console.log(`Unknown command "${cmd}". Type "help" for usage.`);
    }
    rl.prompt();
  });

  rl.on('SIGINT', () => rl.close());
  rl.on('close', () => {
    console.log('\nbye.');
    process.exit(0);
  });
}

function main() {
  const mode = process.argv[2];
  const key = process.argv[3];

  if (mode === 'repl') {
    startRepl(key || null);
    return;
  }

  if (!mode || !key || !['enc', 'dec'].includes(mode)) {
    usage();
    process.exit(1);
  }

  if (!isAlphabetic(key)) {
    console.error('Error: key must contain alphabetic characters only.');
    process.exit(1);
  }

  let stdin = '';
  process.stdin.setEncoding('utf8');
  process.stdin.on('data', (chunk) => {
    stdin += chunk;
  });

  process.stdin.on('end', () => {
    try {
      const result = mode === 'enc' ? encrypt(stdin, key) : decrypt(stdin, key);
      process.stdout.write(result);
    } catch (err) {
      console.error(`Error: ${err.message}`);
      process.exit(1);
    }
  });

  if (process.stdin.isTTY) {
    // No stdin piped in; process immediately.
    process.stdin.emit('end');
  }
}

main();

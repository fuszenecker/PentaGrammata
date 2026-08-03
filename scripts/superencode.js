#!/usr/bin/env node

'use strict';

function usage() {
  console.error('Usage: node superencode.js <enc|dec> <KEY>');
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

function normalizePlainText(input) {
  // Keep everything except line breaks; then apply the requested substitutions.
  return input.replace(/[\r\n]/g, '=').replace(/ /g, '=').replace(/\./g, '+');
}

function denormalizePlainText(input) {
  return input.replace(/=/g, ' ').replace(/\+/g, '.');
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
  const padLength = (rows * cols) - normalized.length;
  const padded = normalized + '='.repeat(padLength);

  const table = [];
  for (let r = 0; r < rows; r += 1) {
    table.push(padded.slice(r * cols, (r + 1) * cols).split(''));
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

  // Strip trailing padding characters (=)
  out = out.replace(/=+$/, '');

  return denormalizePlainText(out);
}

function main() {
  const mode = process.argv[2];
  const key = process.argv[3];

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

# Promptuarium Configuration Format (`PRMT:CFG1`)

This document specifies how a JSON-like configuration (e.g. `appsettings.json`) is stored in a Promptuarium container.
It builds on the [Binary Format Specification](BINARY_FORMAT.md): the container defines the tree, this document defines the meaning of the data and metadata of its nodes.

**Contents**

1. [Notation](#1-notation)
2. [Signature](#2-signature)
3. [Metadata tags](#3-metadata-tags)
4. [Type codes](#4-type-codes)
5. [Tree structure](#5-tree-structure)
6. [Example](#6-example)
7. [Test vector](#7-test-vector)

---

## 1. Notation

* `[d: xxx]` means data with the value `xxx`, `[m: yyy]` means metadata with the value `yyy`.
* **MUST**, **MUST NOT**, **SHOULD** and **MAY** are used as in RFC 2119.
* All multi-byte numbers are little-endian.

## 2. Signature

The root node follows the [file identification convention](BINARY_FORMAT.md#10-file-identification-optional-convention):

* the root has **no data**,
* its metadata is the signature `PRMT:CFG1` (9 bytes),
* its children are the members of the configuration.

The root is an **implicit OBJECT**: it has no TYPE child, its children are NAME nodes.

A file in this format starts with the bytes `69 50 52 4D 54 3A 43 46 47 31`.

## 3. Metadata tags

The metadata of every node (except the root) is exactly one byte, the tag:

| Tag     | Code   | Data of the node |
|---------|--------|------------------|
| `NAME`  | `0x00` | the name of an object member, UTF-8 |
| `TYPE`  | `0x01` | one type code (see [section 4](#4-type-codes)) |
| `ITEM`  | `0x02` | empty (an array element has no name) |
| `VALUE` | `0x03` | the value, encoded according to the type |

A reader MUST reject a node with any other metadata.

## 4. Type codes

The data of a TYPE node is exactly one byte, the type code.

### 4.1 Supported types

| Type          | Code   | VALUE node | Children of the holder |
|---------------|--------|------------|------------------------|
| `NULL`        | `0x00` | none       | — |
| `BOOLEAN`     | `0x01` | 1 byte: `0x00` = false, `0x01` = true | — |
| `OBJECT`      | `0x20` | none       | NAME nodes (the members, in order) |
| `ARRAY`       | `0x21` | none       | ITEM nodes (the elements, in order) |
| `FLOAT64`     | `0x63` | 8 bytes, IEEE 754 binary64 | — |
| `UTF8_STRING` | `0x80` | UTF-8 bytes, may be empty | — |

Rules:

* A `BOOLEAN` value MUST be `0x00` or `0x01`.
* A `FLOAT64` value MUST NOT be NaN or ±Infinity (JSON can not represent them).
* A `UTF8_STRING` value MUST be valid UTF-8.

### 4.2 Reserved types

These codes are reserved for later versions and are **not** supported by `CFG1`:

| Type       | Code   | Type       | Code   | Type       | Code   |
|------------|--------|------------|--------|------------|--------|
| `INT8`     | `0x40` | `UINT8`    | `0x50` | `FLOAT8`   | `0x60` |
| `INT16`    | `0x41` | `UINT16`   | `0x51` | `FLOAT16`  | `0x61` |
| `INT32`    | `0x42` | `UINT32`   | `0x52` | `FLOAT32`  | `0x62` |
| `INT64`    | `0x43` | `UINT64`   | `0x53` |            |        |
| `INT128`   | `0x44` | `UINT128`  | `0x54` | `FLOAT128` | `0x64` |

A reader MUST reject a reserved or unknown type code.

### 4.3 Complete list

```text
Metadata tags:

NAME        = 0x00
TYPE        = 0x01
ITEM        = 0x02
VALUE       = 0x03

Type codes:

NULL        = 0x00
BOOLEAN     = 0x01

OBJECT      = 0x20
ARRAY       = 0x21

INT8        = 0x40   (reserved)
INT16       = 0x41   (reserved)
INT32       = 0x42   (reserved)
INT64       = 0x43   (reserved)
INT128      = 0x44   (reserved)

UINT8       = 0x50   (reserved)
UINT16      = 0x51   (reserved)
UINT32      = 0x52   (reserved)
UINT64      = 0x53   (reserved)
UINT128     = 0x54   (reserved)

FLOAT8      = 0x60   (reserved)
FLOAT16     = 0x61   (reserved)
FLOAT32     = 0x62   (reserved)
FLOAT64     = 0x63
FLOAT128    = 0x64   (reserved)

UTF8_STRING = 0x80
```

## 5. Tree structure

A NAME node (an object member) or an ITEM node (an array element) is a **holder**: it holds one value.

A holder has exactly one TYPE child, as its **first** child, followed by:

| Type | Further children |
|------|------------------|
| `UTF8_STRING`, `FLOAT64`, `BOOLEAN` | exactly one VALUE node |
| `NULL` | none |
| `OBJECT` | zero or more NAME nodes |
| `ARRAY` | zero or more ITEM nodes |

So:

* `{}` is a holder with a single `TYPE OBJECT` child,
* `[]` is a holder with a single `TYPE ARRAY` child,
* arrays may be heterogeneous and may contain objects and arrays,
* the order of the members and of the elements is the order of the children,
* if an object has several members with the same name, the **last one wins** (as in `IConfiguration`); a writer SHOULD NOT write duplicate names.

TYPE and VALUE nodes have no children.

## 6. Example

```json
{
    "key1": "value1",
    "key2": 1.5,
    "key3": true,
    "key4": [ "value41", "value42" ],
    "key5": { "key51": "value51" },
    "key6": null
}
```

```text
[m: PRMT:CFG1]
├── [m: NAME] [d: key1]
│   ├── [m: TYPE] [d: UTF8_STRING]
│   └── [m: VALUE] [d: value1]
├── [m: NAME] [d: key2]
│   ├── [m: TYPE] [d: FLOAT64]
│   └── [m: VALUE] [d: 1.5]
├── [m: NAME] [d: key3]
│   ├── [m: TYPE] [d: BOOLEAN]
│   └── [m: VALUE] [d: 0x01]
├── [m: NAME] [d: key4]
│   ├── [m: TYPE] [d: ARRAY]
│   ├── [m: ITEM]
│   │   ├── [m: TYPE] [d: UTF8_STRING]
│   │   └── [m: VALUE] [d: value41]
│   └── [m: ITEM]
│       ├── [m: TYPE] [d: UTF8_STRING]
│       └── [m: VALUE] [d: value42]
├── [m: NAME] [d: key5]
│   ├── [m: TYPE] [d: OBJECT]
│   └── [m: NAME] [d: key51]
│       ├── [m: TYPE] [d: UTF8_STRING]
│       └── [m: VALUE] [d: value51]
└── [m: NAME] [d: key6]
    └── [m: TYPE] [d: NULL]
```

## 7. Test vector

The canonical encoding of the example above (157 bytes):

```text
69 50 52 4d 54 3a 43 46 47 31 44 6b 65 79 31 21
00 41 80 21 01 c6 76 61 6c 75 65 31 21 03 84 6b
65 79 32 21 00 41 63 21 01 c8 00 00 00 00 00 00
f8 3f 21 03 84 6b 65 79 33 21 00 41 01 21 01 c1
01 21 03 84 6b 65 79 34 21 00 41 21 21 01 e1 02
41 80 21 01 c7 76 61 6c 75 65 34 31 21 03 a1 02
41 80 21 01 c7 76 61 6c 75 65 34 32 21 03 80 84
6b 65 79 35 21 00 41 20 21 01 c5 6b 65 79 35 31
21 00 41 80 21 01 c7 76 61 6c 75 65 35 31 21 03
80 84 6b 65 79 36 21 00 41 00 21 01 00
```

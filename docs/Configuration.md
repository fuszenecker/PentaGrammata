# Binary format of the configuration:

`[d: xxx]` means data with value of `xxx`, `[m: yyy]` means metadata with value of `yyy`.

A configuration file, like `appsettings.json` is encoded as:

```json
{
    "key1": "value1",
    "key2": value2,
    "key3": true,
    "key4": [
        "value41", "value42"
    ],
    "key5": {
        "key51": "value51"
    },
    "key6": null
}
```

Metadata tags:

- NAME = 0,
- TYPE = 1,
- VALUE = 3,

- NULL = 0x40,
- BOOLEAN = 0x41,

- INT8 = 0x50,
- INT16 = 0x51,
- INT32 = 0x52,
- INT64 = 0x53,
- INT128 = 0x54,

- UINT8 = 0x60,
- UINT16 = 0x61,
- UINT32 = 0x62,
- UINT64 = 0x63,
- UINT128 = 0x64,

- FLOAT8 = 0x70,
- FLOAT16 = 0x71,
- FLOAT32 = 0x72,
- FLOAT64 = 0x73,
- FLOAT128 = 0x74,

- UTF8_STRING = 0x80,

[m: PRMT:CFG1]
- [m: NAME] [d: key1]
  - [m: TYPE] [d: UTF8_STRING]
  - [m: VALUE] [d: value1]
- [m: NAME] [d: key2]
  - [m: TYPE] [d: FLOAT64]
  - [m: VALUE] [d: value2]
- [m: NAME] [d: key3]
  - [m: TYPE] [d: BOOLEAN]
  - [m: VALUE] [d: 1]
- [m: NAME] [d: key4]
  - [m: TYPE] [d: UTF8_STRING]
  - [m: VALUE] [d: value41]
  - [m: VALUE] [d: value42]
- [m: NAME] [d: key5]
  - [m: NAME] [d: key51]
    - [m: TYPE] [d: UTF8_STRING]
    - [m: VALUE] [d: value41]
- [m: NAME] [d: key6]
  - [m: TYPE] [d: NULL]

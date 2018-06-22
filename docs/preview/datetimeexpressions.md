# Support DateTime type in expressions

## New type

This change will add support for a DateTime type to the expression runtime.

With this change, the full set of supported types will be:
- `Array`
- `Boolean`
- `DateTime`
- `Null`
- `Number`
- `Object`
- `String`
- `Version`

## Equality/compare support

DateTime values will work with the following compare/equality functions:
- `eq`
- `ne`
- `lt`
- `le`
- `gt`
- `ge`

Internally we will use DateTimeOffset and treat 2018-01-02 00:00:00-05:00 and 2018-01-02 05:00:00Z as equal (same point in time, viewed in a different offset). This is different from how .Net treats DateTime comparisons, but consistent with how .Net treats DateTimeOffset comparisons.

## Implicit conversion

Implicit conversion can happen within the expression runtime.

For example, equality functions `eq` and `ne` try to convert the right-hand argument to match the type of the left-hand argument. If the conversion fails, equality is considered false. And comparison functions `lt`, `le`, `gt`, `ge` require the right-hand argument to convert to the type of the left-hand argument. Otherwise an error bubbles.

The following DateTime conversions are supported:

| From | To | Result |
| --- | --- | --- |
| `DateTime` | `Boolean` | True |
| `DateTime` | `String` | 2018-01-02 21:45:21-05:00 |
| `String` | `DateTime` | These strings will parse:<br/>2018-01-02 21:45:21<br/>2018-01-02 21:45:21-05:00<br/>2018-01-02 21:45:21Z (Z indicates UTC)<br/>2018/01/02 21:45:21 (slashes supported)<br/>2018-01-02T21:45:21 (T supported) |

## Format function

Extend the format function to support format specifiers for DateTime values.

- `yyyy`
- `yy`
- `MM`
- `M`
- `dd`
- `d`
- `HH`
- `H`
- `mm`
- `m`
- `ss`
- `s`
- `K` - The time zone offset. For example `-05:00` in `2018-01-02 00:00:00-05:00`. Or `Z` in the UTC time `2018-01-02 05:00:00Z`.

## New functions, context

Add a new function `now()` that returns the local time based on the account setting.

Add `plan.startTime` to the expression context.

Add `stage.startTime` to the expression context (`stage` indicates whatever stage you are in).

## Parse functions?

Is now a good time go ahead and add parse functions for built-in types? For example:

- `parseBool(string)`
- `parseDateTime(string)`
- `parseNumber(string)`
- `parseVersion(string)`

// Decision R-12: always en-US, whatever the browser's language. Same output as the Razor UI's
// DisplayFor on a [DataType(DataType.Currency)] decimal ("{0:C}"): $19.50, $1,000.00.
const price = new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' })

export function formatPrice(value: number): string {
  return price.format(value)
}

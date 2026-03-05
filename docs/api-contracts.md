# API contracts

## Transfer.Orchestrator

- **POST /transfers**  
  Body: `{ userId, recipientId, sourceCurrency, sourceAmount, destinationCurrency }`  
  Response: `{ transferId, status, correlationId }` (202 Accepted)

- **GET /transfers/{transferId}**  
  Response: `TransferDto` (200) or 404

## Rails.Nigeria

- **POST /ng/collect**  
  Headers: `Idempotency-Key`, `X-Correlation-ID`  
  Body: `{ transferId, amount, currency, bankAccountRef }`  
  Response: `{ referenceId, status }`

- **GET /ng/transactions/{referenceId}**  
  Response: `{ referenceId, status, externalReference }`

## Rails.UnitedStates

- **POST /us/payout**  
  Headers: `Idempotency-Key`, `X-Correlation-ID`  
  Body: `{ transferId, amount, currency, bankAccountRef }`  
  Response: `{ referenceId, status }`

- **GET /us/transactions/{referenceId}**  
  Response: `{ referenceId, status, externalReference }`

## FX.Service

- **POST /fx/quote**  
  Body: `{ transferId, sourceCurrency, destinationCurrency, sourceAmount }`  
  Response: `{ quoteId, rate, expiresAt, estimatedDestinationAmount, feeAmount }`

- **POST /fx/accept**  
  Body: `{ transferId, quoteId }`  
  Response: `{ acceptedQuoteId, expiresAt }` or 400 if expired

- **POST /fx/execute**  
  Headers: `Idempotency-Key`, `X-Correlation-ID`  
  Body: `{ transferId, acceptedQuoteId }`  
  Response: `{ executionId, status, filledAmount, rate }`

## Ledger.Service

- **POST /ledger/bootstrap**  
  Creates required accounts (idempotent). Local/setup only.

- **POST /ledger/posting**  
  Headers: `Idempotency-Key`, `X-Correlation-ID`  
  Body: `{ transferId, operationType, entries: [{ account, debit, credit, currency }] }`  
  Response: `{ postingId, success, errorCode? }`

- **POST /ledger/reserve**  
  Headers: `Idempotency-Key`, `X-Correlation-ID`  
  Body: `{ transferId, fromAccount, toAccount, amount, currency }`  
  Response: `{ reservationId, success, errorCode? }`

- **POST /ledger/release**  
  Body: `{ reservationId }`  
  Response: `{ success, errorCode? }`

- **GET /ledger/balances/{account}**  
  Response: `{ account, currency, balance }`

#include "../Libmem.NET.h"

using namespace System;
namespace Libmem::NET {

LibmemException::LibmemException(String^ operation,String^ message)
    : InvalidOperationException(message),operation_(operation) {
    if(String::IsNullOrWhiteSpace(operation))
        throw gcnew ArgumentException("Operation must not be empty.", "operation");
}
LibmemException::LibmemException(String^ operation,String^ message,Exception^ innerException)
    : InvalidOperationException(message,innerException),operation_(operation) {
    if(String::IsNullOrWhiteSpace(operation))
        throw gcnew ArgumentException("Operation must not be empty.", "operation");
}
String^ LibmemException::Operation::get() { return operation_; }

} // namespace Libmem::NET

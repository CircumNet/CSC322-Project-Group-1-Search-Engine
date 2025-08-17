//
// FILE: Models/ErrorViewModel.cs
//
// Basic error view model
//
namespace SearchEngine.Models
{
    public class ErrorViewModel
    {
        public string RequestId { get; set; }
        public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
    }
}

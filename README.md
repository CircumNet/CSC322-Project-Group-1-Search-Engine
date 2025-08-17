# SearchEngine Project 
# Group 1

This project is a C# search engine application built using ASP.NET Core MVC. It can index and search various document types, including `.txt`, `.pdf`, `.docx`, `.xlsx`, `.pptx`, `.html`, and `.xml`. The application uses an inverted index for efficient searching and a BM25 ranking algorithm to provide relevant results.

---

## 👥 Group Members
The following are the memebers, matriculation numbers and roles.
* Aweroro Ayomide Abdullahi&nbsp;&nbsp;&nbsp;&nbsp;**210805094**&nbsp;&nbsp;&nbsp;&nbsp; _DocumentRepresentation_
* Buraimoh Mariam Ololade&nbsp;&nbsp;&nbsp;&nbsp;**210805063**&nbsp;&nbsp;&nbsp;&nbsp;_QueryRepresentation_
* Ashidi Joy&nbsp;&nbsp;&nbsp;&nbsp;**210805062**&nbsp;&nbsp;&nbsp;&nbsp;_Graphical User Interface_
* Okosun Oselumhen Samuel&nbsp;&nbsp;&nbsp;&nbsp;**210805033**&nbsp;&nbsp;&nbsp;&nbsp;_SearchEngineAPI Developer_


## 🚀 Features

* **Multi-format Indexing:** Indexes a variety of document formats.
* **Boolean Queries:** Supports basic boolean search logic with `AND`, `OR`, and `NOT` operators.
* **Phrase Search:** Allows searching for exact phrases by enclosing them in double quotes `" "`.
* **Ranked Results:** Ranks search results based on a relevance score calculated using the **BM25 algorithm**.
* **RESTful API:** Exposes the search functionality via a `POST` API endpoint at `/api/search`.

---

## 🛠️ Requirements & Installation

### Prerequisites

* **.NET 8 SDK:** This project is built on .NET 8. Ensure you have the SDK installed on your system.
* **Visual Studio 2022:** The project is configured for Visual Studio 2022, but you can also use Visual Studio Code or other compatible IDEs.
* **Libraries:** The application relies on several third-party libraries for document parsing. These are automatically managed by NuGet. The key libraries include:
    * `Spire.Presentation` and `Spire.Doc` for PowerPoint and Word `.doc` files.
    * `HtmlAgilityPack` for parsing HTML files.
    * `iText` for reading PDF files.
    * `Open-XML-SDK` for `.docx` files.
    * `NPOI` for Excel `.xlsx` and `.xls` files.

### Installation Steps

1.  **Clone the Repository:**
    ```bash
    git clone [https://github.com/CircumNet/CSC322-Project-Group-1-Search-Engine.git]([https://github.com/your-repo/your-project.git](https://github.com/CircumNet/CSC322-Project-Group-1-Search-Engine.git))
    cd SearchEngineWebAPI
    ```
2.  **Restore NuGet Packages:**
    Open the solution in Visual Studio. The NuGet packages will be restored automatically. If they aren't, you can manually restore them via the command line:
    ```bash
    dotnet restore
    ```
3.  **Place Documents to be Indexed:**
    The search engine is configured to automatically index all files placed in the `docs` folder at startup. Create a `docs` folder in the root directory of the project if it doesn't exist, and place your supported documents inside.
4.  **Run the Application:**
    You can run the application directly from Visual Studio by pressing `F5` or from the command line:
    ```bash
    dotnet run
    ```
    The application will start, and a browser window will open, showing the search interface.

---

## 📖 Usage

### Web Interface

After running the application, navigate to the search page. You can enter your query in the search bar.

### Query Syntax

The search bar supports the following syntax:

* **Simple Keywords:** Enter single words to find documents containing those words. Example: `hello world`
* **Boolean Operators:**
    * `AND`: Finds documents that contain all specified terms. Example: `cat AND dog`
    * `OR`: Finds documents that contain at least one of the specified terms. Example: `apple OR orange`
    * `NOT`: Excludes documents containing a specific term. Example: `python NOT java`
* **Parentheses:** Use parentheses to group terms and control the order of operations. Example: `(computer AND science) OR technology`
* **Phrase Search:** Enclose a phrase in double quotes to search for that exact sequence of words. Example: `"data structure"`
* **Note:** The operators are case-insensitive.

### BM25 Ranking

The BM25 algorithm is used to rank the documents by relevance. The score is calculated based on several factors:
* **Term Frequency (tf):** The number of times a term appears in a document.
* **Inverse Document Frequency (idf):** A measure of how rare or common a term is across the entire document collection.
* **Document Length:** A normalization factor that accounts for longer documents potentially having higher term frequencies by chance.

The formula for the BM25 score of a document $D$ for a query $Q$ is:

$$Score(D, Q) = \sum_{i=1}^{n} \text{IDF}(q_i) \cdot \frac{\text{tf}(q_i, D) \cdot (k_1 + 1)}{\text{tf}(q_i, D) + k_1 \cdot (1 - b + b \cdot \frac{|D|}{\text{avgdl}})}$$

Where:
* $\text{tf}(q_i, D)$ is the term frequency of query term $q_i$ in document $D$.
* $|D|$ is the length of the document $D$.
* $\text{avgdl}$ is the average document length in the collection.
* $k_1$ and $b$ are tuning parameters, with default values of $1.5$ and $0.75$ respectively.
* $\text{IDF}(q_i)$ is the Inverse Document Frequency, calculated as:
    $$\text{IDF}(q_i) = \log\left(\frac{N - \text{df}(q_i) + 0.5}{\text{df}(q_i) + 0.5} + 1.0\right)$$
    where $N$ is the total number of documents and $\text{df}(q_i)$ is the document frequency of term $q_i$ (the number of documents containing the term).

### API Endpoint

For programmatic access, you can make a `POST` request to the `/api/search` endpoint with the query string in the request body.

**Endpoint:** `POST /api/search`
**Request Body:** `string` (e.g., `"test query"` )
**Response:** `JSON` array of `SearchResultItem` objects.

Example `curl` request:

```bash
curl -X POST -H "Content-Type: application/json" -d '"test query"' https://localhost:7001/api/search

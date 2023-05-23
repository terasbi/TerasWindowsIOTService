// --------------------------------------------------------------------------------------------------------------------
// <copyright file="CircularList.cs" company="Microsoft">
//   THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
//   IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
//   FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL
//   THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR
//   OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE,
//   ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR
//   OTHER DEALINGS IN THE SOFTWARE.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------
namespace LCS_IoT_Svc
{
    using System.Collections;
    using System.Collections.Generic;

    /// <summary>
    /// Circular List
    /// </summary>
    /// <typeparam name="T">Type of Circular List</typeparam>
    /// <seealso cref="System.Collections.Generic.List{T}" />
    public class CircularList<T> : List<T> where T : class
    {
        /// <summary>
        /// The lock object
        /// </summary>
        private static readonly object LockObj = new object();

        /// <summary>
        /// The local enumerator
        /// </summary>
        private IEnumerator localEnumerator;

        /// <summary>
        /// Initializes a new instance of the <see cref="CircularList{T}" /> class.
        /// </summary>
        /// <param name="capacity">The capacity.</param>
        public CircularList(int capacity) : base(capacity)
        {
        }

        /// <summary>
        /// Gets the next.
        /// </summary>
        /// <returns>Returns the next element in the list in cycling manner</returns>
        public T GetSafeNext()
        {
            lock (LockObj)
            {
                this.localEnumerator = this.localEnumerator ?? this.GetEnumerator();
                if (!this.localEnumerator.MoveNext())
                {
                    this.localEnumerator.Reset();
                    this.localEnumerator.MoveNext();
                }

                return (T)this.localEnumerator.Current;
            }
        }
    }
}
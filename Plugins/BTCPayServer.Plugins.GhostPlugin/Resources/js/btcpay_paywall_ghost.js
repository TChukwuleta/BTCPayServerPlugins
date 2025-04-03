class BTCPayGatedContent extends HTMLElement {
    constructor() {
        super();
        this.attachShadow({ mode: "open" });
        this.uniqueId = this.generateUniqueId();
        const container = document.createElement("div");
        container.innerHTML = `
      <style>
        #content { display: none; }
        #payButton {
          background: green;
          color: white;
          padding: 10px 15px;
          border: none;
          cursor: pointer;
          font-size: 16px;
          border-radius: 5px;
        }
      </style>

      <div id="paySection">
        <p>Unlock this premium content content</p>
        <button id="payButton">Unlock with Bitcoin</button>
      </div>
      <div id="content">
        <slot></slot>
      </div>
    `;

        this.shadowRoot.appendChild(container);

        if (localStorage.getItem("paywall_unlocked_" + this.uniqueId) === "true") {
            this.unlockContent();
        }

        container.querySelector("#payButton").addEventListener("click", (event) => {
            event.preventDefault();
            event.target.disabled = true;
            event.target.textContent = "Loading...";

            const price = this.getAttribute("data-price");
            this.handleBitcoinPayment(price, event.target);
        });
    }

    generateUniqueId() {
        const pageUrl = window.location.pathname;
        const allPaywalls = document.querySelectorAll('btcpay-gated-content');
        let index = -1;
        for (let i = 0; i < allPaywalls.length; i++) {
            if (allPaywalls[i] === this) {
                index = i;
                break;
            }
        }

        if (index === -1) {
            let node = this;
            let path = [];

            while (node && node.parentNode) {
                let siblings = Array.from(node.parentNode.children);
                path.unshift(siblings.indexOf(node));
                node = node.parentNode;
            }

            const pathString = path.join('_');
            return `pw_${pageUrl.replace(/\W+/g, '_')}_path_${pathString}`;
        }
        return `pw_${pageUrl.replace(/\W+/g, '_')}_${index}`;
    }

    getDomPath() {
        let element = this;
        let path = '';
        while (element.parentNode) {
            const siblings = Array.from(element.parentNode.children);
            const index = siblings.indexOf(element);
            const tag = element.tagName.toLowerCase();
            path = `/${tag}[${index}]${path}`;
            element = element.parentNode;

            if (path.split('/').length > 6) break;
        }
        return path;
    }

    paymentCompleted() {
        localStorage.setItem("paywall_unlocked_" + this.uniqueId, "true");
        this.unlockContent();
    }

    unlockContent() {
        this.shadowRoot.querySelector("#paySection").style.display = "none";
        this.shadowRoot.querySelector("#content").style.display = "block";
    }

    handleBitcoinPayment(amount, button) {
        const url = BTCPAYSERVER_URL + "/plugins/" + BTCPAYSERVER_STORE_ID + "/ghost/api/paywall/create-invoice?amount=" + amount;
        fetch(url, {
            method: "GET",
            headers: { "Content-Type": "application/json" }
        })
            .then(response => response.json())
            .then(data => {
                console.log("Invoice created:", data);
                if (data.id) {
                    this.showBTCPayModal(data);
                }
                else {
                    button.disabled = false;
                    button.textContent = "Unlock with Bitcoin";
                    alert("Error: Failed to generate invoice. Please contact admin");
                }
            })
            .catch(error => {
                button.disabled = false;
                button.textContent = "Unlock with Bitcoin";
                console.error("Payment initiation failed. Error:", error);
                alert("Error: Payment initiation failed. Please try again.");
            });
    }


    showBTCPayModal = function (data) {
        console.log('Triggered showBTCPayModal()');
        if (data.id == undefined) {
            console.error('No invoice id provided, aborting.');
        }
        window.btcpay.setApiUrlPrefix(BTCPAYSERVER_URL);
        window.btcpay.showInvoice(data.id);

        let invoice_paid = false;
        const self = this;
        window.btcpay.onModalReceiveMessage((event) => {
            if (isObject(event.data)) {
                if (event.data.status) {
                    switch (event.data.status.toLowerCase()) {
                        case 'complete':
                        case 'paid':
                        case 'confirmed':
                        case 'processing':
                        case 'settled':
                            invoice_paid = true;
                            console.log('Invoice paid.');
                            self.paymentCompleted();
                            break;
                        case 'expired':
                            window.btcpay.hideFrame();
                            console.error('Invoice expired.');
                            break;
                        case 'invalid':
                            window.btcpay.hideFrame();
                            console.error('Invalid invoice.');
                            break;
                        default:
                            console.error('Unknown status: ' + event.data.status);
                    }
                }
            } else {
                if (event.data === 'close') {
                    if (invoice_paid === true) {
                        self.paymentCompleted();
                        console.log('Invoice paid.');
                    }
                    console.log('Modal closed.')
                }
            }
        });
        const isObject = obj => {
            return Object.prototype.toString.call(obj) === '[object Object]'
        }
    };
}

customElements.define("btcpay-gated-content", BTCPayGatedContent);


document.addEventListener("DOMContentLoaded", function () {

    var extLinks = document.querySelectorAll('a[href*="://"]');
    for (var i = 0; i < extLinks.length; i++) {
        if (!extLinks[i].href.startsWith(window.location.origin)) {
            extLinks[i].setAttribute('target', '_blank');
        }
    }

    loadModalScript();
    console.log("Script Loaded");
});


const loadModalScript = () => {
    const script = document.createElement('script');
    script.src = BTCPAYSERVER_URL + '/plugins/' + BTCPAYSERVER_STORE_ID + '/ghost/api/btcpay-ghost.js';
    document.head.appendChild(script);
    script.onload = function () {
        console.log('External modal script loaded successfully.');
    };
    script.onerror = function () {
        console.error('Error loading the external modal script.');
    };
}
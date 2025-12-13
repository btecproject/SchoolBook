// Setting Page JavaScript
document.addEventListener('DOMContentLoaded', function() {
    initializeSettingsPage();
});

function initializeSettingsPage() {
    setupAlertSystem();
    setupButtonInteractions();
    setupCardAnimations();
    setupRecoveryCodeWarning();
}

// Alert System
function setupAlertSystem() {
    const alerts = document.querySelectorAll('.alert');

    alerts.forEach(alert => {
        // Auto-dismiss success alerts after 5 seconds
        if (alert.classList.contains('alert-success')) {
            setTimeout(() => {
                dismissAlert(alert);
            }, 5000);
        }

        // Add click event to close buttons
        const closeBtn = alert.querySelector('.btn-close');
        if (closeBtn) {
            closeBtn.addEventListener('click', function() {
                dismissAlert(alert);
            });
        }
    });
}

function closeAlert(button) {
    const alert = button.closest('.alert');
    dismissAlert(alert);
}

function dismissAlert(alert) {
    if (!alert) return;

    alert.style.opacity = '0';
    alert.style.transform = 'translateY(-10px)';
    alert.style.height = alert.offsetHeight + 'px';

    setTimeout(() => {
        alert.style.height = '0';
        alert.style.marginBottom = '0';
        alert.style.padding = '0';
    }, 300);

    setTimeout(() => {
        alert.remove();
    }, 600);
}

// Button Interactions
function setupButtonInteractions() {
    const regenerateBtn = document.getElementById('regenerate-btn');
    if (regenerateBtn) {
        regenerateBtn.addEventListener('click', handleRegenerateCodes);
    }

    const actionButtons = document.querySelectorAll('.card-action-btn');
    actionButtons.forEach(btn => {
        btn.addEventListener('click', function(e) {
            // Add click animation
            this.style.transform = 'scale(0.95)';
            setTimeout(() => {
                this.style.transform = '';
            }, 150);

            // Special handling for dangerous actions
            if (this.classList.contains('btn-danger')) {
                if (!confirm('Are you sure you want to disable Two-Factor Authentication? This will reduce your account security.')) {
                    e.preventDefault();
                }
            }
        });
    });
}

// Recovery Codes Handling
function handleRegenerateCodes(e) {
    const confirmation = confirm('WARNING: This will invalidate all your current recovery codes. Make sure you have saved them before proceeding.\n\nAre you sure you want to generate new recovery codes?');

    if (!confirmation) {
        e.preventDefault();
        return;
    }

    // Show loading state
    const btn = e.target.closest('.btn-regenerate') || e.target;
    const originalText = btn.innerHTML;
    btn.innerHTML = '<i class="regenerate-icon">⏳</i> Generating...';
    btn.disabled = true;

    // Add loading animation
    btn.classList.add('pulse');

    // Revert after 2 seconds if form submission fails
    setTimeout(() => {
        if (btn.disabled) {
            btn.innerHTML = originalText;
            btn.disabled = false;
            btn.classList.remove('pulse');
        }
    }, 2000);
}

function setupRecoveryCodeWarning() {
    const recoveryCount = document.querySelector('.count-number');
    if (recoveryCount) {
        const count = parseInt(recoveryCount.textContent);
        if (count <= 3) {
            const warningElement = document.querySelector('.codes-warning');
            if (warningElement) {
                warningElement.style.color = 'var(--danger-color)';
                warningElement.innerHTML = '<i class="warning-icon">🚨</i> <strong>Critical:</strong> Generate new codes immediately!';
                warningElement.classList.add('pulse');
            }
        }
    }
}

// Card Animations
function setupCardAnimations() {
    const cards = document.querySelectorAll('.settings-card');

    cards.forEach((card, index) => {
        // Staggered animation on load
        card.style.opacity = '0';
        card.style.transform = 'translateY(20px)';

        setTimeout(() => {
            card.style.transition = 'opacity 0.5s ease, transform 0.5s ease';
            card.style.opacity = '1';
            card.style.transform = 'translateY(0)';
        }, index * 100);

        // Hover effect enhancement
        card.addEventListener('mouseenter', function() {
            const icon = this.querySelector('.card-icon');
            if (icon) {
                icon.style.transform = 'scale(1.1) rotate(5deg)';
            }
        });

        card.addEventListener('mouseleave', function() {
            const icon = this.querySelector('.card-icon');
            if (icon) {
                icon.style.transform = '';
            }
        });
    });
}

// Utility Functions
function showToast(message, type = 'success') {
    const toast = document.createElement('div');
    toast.className = `toast toast-${type}`;
    toast.innerHTML = `
        <i class="toast-icon">${type === 'success' ? '✅' : '❌'}</i>
        <span>${message}</span>
        <button class="toast-close" onclick="this.parentElement.remove()">&times;</button>
    `;

    // Add toast styles if not already present
    if (!document.querySelector('#toast-styles')) {
        const style = document.createElement('style');
        style.id = 'toast-styles';
        style.textContent = `
            .toast {
                position: fixed;
                top: 20px;
                right: 20px;
                padding: 15px 20px;
                border-radius: var(--border-radius);
                display: flex;
                align-items: center;
                gap: 10px;
                z-index: 1000;
                animation: slideInRight 0.3s ease;
                box-shadow: 0 4px 15px rgba(0,0,0,0.1);
            }
            .toast-success { background: #d4edda; color: #155724; border-left: 4px solid #2a9d8f; }
            .toast-error { background: #f8d7da; color: #721c24; border-left: 4px solid #e63946; }
            .toast-close { background: none; border: none; font-size: 1.2rem; cursor: pointer; margin-left: auto; }
            @keyframes slideInRight {
                from { transform: translateX(100%); opacity: 0; }
                to { transform: translateX(0); opacity: 1; }
            }
        `;
        document.head.appendChild(style);
    }

    document.body.appendChild(toast);

    // Auto-remove after 5 seconds
    setTimeout(() => {
        if (toast.parentElement) {
            toast.style.opacity = '0';
            toast.style.transform = 'translateX(100%)';
            setTimeout(() => toast.remove(), 300);
        }
    }, 5000);
}

// Export for global use
window.settingUtils = {
    showToast,
    dismissAlert,
    closeAlert
};
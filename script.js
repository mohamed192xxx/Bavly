const menuToggle = document.querySelector('.menu-toggle');
const navLinks = document.querySelector('.nav-links');

if (menuToggle && navLinks) {
  menuToggle.addEventListener('click', () => {
    const isOpen = navLinks.classList.toggle('open');
    navLinks.style.display = isOpen ? 'flex' : '';
    if (isOpen) {
      navLinks.style.position = 'absolute';
      navLinks.style.top = '74px';
      navLinks.style.left = '4vw';
      navLinks.style.right = '4vw';
      navLinks.style.background = '#111029';
      navLinks.style.border = '1px solid #302c58';
      navLinks.style.borderRadius = '14px';
      navLinks.style.padding = '1rem';
      navLinks.style.flexDirection = 'column';
      navLinks.style.zIndex = '25';
    }
  });
}
